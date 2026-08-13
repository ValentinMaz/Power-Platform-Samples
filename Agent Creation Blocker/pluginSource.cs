using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace LogAgentCreationAttempt
{
    // Dataverse plug-in that fires on the Create message for the "bot" entity
    // (Copilot Studio agent). On every agent-creation attempt it:
    //   1. Identifies the initiating user and reads their email.
    //   2. Allows the creation if the email matches the allow-list stored in the
    //      Environment Variable "ptricks_AgentCreationAllowedEmailContains"; otherwise blocks it.
    //   3. POSTs an audit payload to a Power Automate Flow (URL from the Environment Variable
    //      "ptricks_AgentCreationLoggingFlowUrl") regardless of the block decision. The HTTP
    //      call never prevents the block from being enforced.
    public class LogAgentCreationAttemptPlugin : IPlugin
    {
        // Snapshot of the initiating user's systemuser record, read once per execution and
        // reused for both the allow-list check and the audit payload.
        private sealed class InitiatingUserDetails
        {
            public string InternalEmailAddress { get; set; }
            public string FullName { get; set; }
            public string DomainName { get; set; }
            // Numeric value of the accessmode OptionSet (0 = Read-Write, 1 = Administrative, etc.).
            public int? AccessMode { get; set; }
            // Entra ID (Azure AD) object identifier (GUID as string).
            public string EntraObjectId { get; set; }
        }

        // JSON contract POSTed to the logging Flow — the Flow parses this and writes it to the
        // audit store, so the property names must stay stable.
        [DataContract]
        private sealed class FlowLogPayload
        {
            [DataMember(Name = "attemptedOnUtc")]
            public string AttemptedOnUtc { get; set; }

            [DataMember(Name = "agentDisplayName")]
            public string AgentDisplayName { get; set; }

            [DataMember(Name = "requestFingerprint")]
            public string RequestFingerprint { get; set; }

            [DataMember(Name = "messageName")]
            public string MessageName { get; set; }

            [DataMember(Name = "primaryEntity")]
            public string PrimaryEntity { get; set; }

            // Plug-in pipeline stage: 10 = PreValidation, 20 = PreOperation, 40 = PostOperation.
            [DataMember(Name = "stage")]
            public int Stage { get; set; }

            [DataMember(Name = "isInTransaction")]
            public bool IsInTransaction { get; set; }

            [DataMember(Name = "correlationId")]
            public string CorrelationId { get; set; }

            [DataMember(Name = "requestId")]
            public string RequestId { get; set; }

            [DataMember(Name = "organizationId")]
            public string OrganizationId { get; set; }

            // --- Initiating user (the human who clicked "Create agent") ---

            [DataMember(Name = "initiatingUserId")]
            public string InitiatingUserId { get; set; }

            [DataMember(Name = "initiatingUserInternalEmailAddress")]
            public string InitiatingUserInternalEmailAddress { get; set; }

            [DataMember(Name = "initiatingUserFullName")]
            public string InitiatingUserFullName { get; set; }

            [DataMember(Name = "initiatingUserDomainName")]
            public string InitiatingUserDomainName { get; set; }

            [DataMember(Name = "initiatingUserAccessMode")]
            public int? InitiatingUserAccessMode { get; set; }

            [DataMember(Name = "initiatingUserEntraObjectId")]
            public string InitiatingUserEntraObjectId { get; set; }

            // The "effective" Dataverse user running the operation — may differ from the
            // initiating user under impersonation or a service account.
            [DataMember(Name = "userId")]
            public string UserId { get; set; }

            [DataMember(Name = "shouldBlock")]
            public bool ShouldBlock { get; set; }

            [DataMember(Name = "blockReason")]
            public string BlockReason { get; set; }
        }

        // Dataverse logical name for a Copilot Studio agent.
        private const string BotEntityLogicalName = "bot";

        // Fallback message surfaced to the user when a creation attempt is blocked and no
        // custom message is configured via the BlockMessage Environment Variable.
        private const string BlockMessage = "Agent creation is not allowed in this environment and has been blocked by a Dataverse plugin. Please contact your administrator for more information.";

        // Environment Variable schema names. The values they resolve to are admin-editable
        // without redeploying this assembly.
        private const string FlowUrlEnvironmentVariableSchemaName = "ptricks_AgentCreationLoggingFlowUrl";
        private const string AllowedEmailContainsEnvironmentVariableSchemaName = "ptricks_AgentCreationAllowedEmailContains";
        private const string BlockMessageEnvironmentVariableSchemaName = "ptricks_AgentCreationBlockMessage";

        // Deadline for the Flow HTTP call so a slow endpoint cannot stall the Dataverse pipeline.
        private const int FlowTimeoutMs = 4000;

        // Attribute names checked in order for the agent's human-readable name; first non-blank wins.
        private static readonly string[] DisplayNameCandidateColumns = { "displayname", "name", "schemaname" };

        // A single shared HttpClient — creating one per execution would exhaust sockets under
        // load. Plug-ins are stateless between executions, but this static survives for the
        // lifetime of the sandbox worker process.
        private static readonly HttpClient FlowHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(FlowTimeoutMs)
        };

        public void Execute(IServiceProvider serviceProvider)
        {
            // ITracingService writes to the Plug-in Trace Log entity — essential for diagnosis
            // without a debugger.
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            tracingService?.Trace(
                "LogAgentCreationAttemptPlugin: Start. Message={0}, Entity={1}, Stage={2}, IsInTransaction={3}, Depth={4}, CorrelationId={5}",
                context?.MessageName,
                context?.PrimaryEntityName,
                context?.Stage,
                context?.IsInTransaction,
                context?.Depth,
                context?.CorrelationId);

            if (context == null)
            {
                return;
            }

            // Create passes a single Entity in "Target". The entity check is a defensive guard
            // against a mis-configured step registration.
            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
                || !string.Equals(target.LogicalName, BotEntityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // System-level (privileged) service via null user id — bypasses row-level security
            // so the plug-in can always read systemuser records and environment variables.
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var systemService = serviceFactory?.CreateOrganizationService(null);

            var attemptedOnUtc = DateTime.UtcNow;

            // Stable ID tying the trace log entry to the Flow audit record.
            var requestFingerprint = GetRequestFingerprint(context);

            // InitiatingUserId is the original human even under impersonation.
            var initiatingUser = GetInitiatingUserDetails(systemService, context, tracingService);

            // Comma-separated allow-list, e.g. "@contoso.com,@fabrikam.com". Allowed if the email
            // contains ANY allow-list substring. An empty or unreadable allow-list means nobody
            // is permitted (fail-closed full-block mode).
            var allowedEmailContainsValues = ParseContainsAllowList(
                GetEnvironmentVariableValue(systemService, AllowedEmailContainsEnvironmentVariableSchemaName, tracingService));

            var isAllowedByEmailContains = EmailMatchesContainsAllowList(initiatingUser?.InternalEmailAddress, allowedEmailContainsValues);
            var shouldBlock = !isAllowedByEmailContains;
            var blockReason = shouldBlock
                ? "Initiating user email did not match any configured allow-list contains values."
                : "Initiating user email matched a configured allow-list contains value.";

            tracingService?.Trace(
                "LogAgentCreationAttemptPlugin: Initiating user evaluated. Email={0}, AllowListCount={1}, IsAllowedByContains={2}, ShouldBlock={3}",
                initiatingUser?.InternalEmailAddress,
                allowedEmailContainsValues.Count,
                isAllowedByEmailContains,
                shouldBlock);

            // May be blank at PreValidation before Dataverse fills default fields.
            var displayName = ResolveDisplayName(target) ?? "(display name not provided)";

            // Log BEFORE throwing the block: a transaction rollback would discard Dataverse-based
            // logging, but this outbound HTTP call survives it. A failed Flow call is non-fatal —
            // the block decision is independent of logging.
            TrySendToFlow(
                systemService,
                tracingService,
                context,
                displayName,
                requestFingerprint,
                initiatingUser,
                shouldBlock,
                blockReason,
                attemptedOnUtc);

            if (shouldBlock)
            {
                // Admin-customizable message via Environment Variable; falls back to the hardcoded
                // default when unset or unreadable (fail-safe — a block always has a message).
                var blockMessage = GetEnvironmentVariableValue(systemService, BlockMessageEnvironmentVariableSchemaName, tracingService);
                if (string.IsNullOrWhiteSpace(blockMessage))
                {
                    blockMessage = BlockMessage;
                }

                tracingService?.Trace("LogAgentCreationAttemptPlugin: Throwing block exception.");

                // Cancels the Dataverse transaction and surfaces the friendly message. The audit
                // call above already fired, so the log entry exists regardless.
                throw new InvalidPluginExecutionException(blockMessage);
            }

            tracingService?.Trace("LogAgentCreationAttemptPlugin: Completed — initiating user is on the allow-list, not blocking.");
        }

        // Builds the JSON payload and POSTs it to the Flow. Never throws — it runs before the
        // optional block exception, so all failures (including timeouts) are caught and traced.
        private static void TrySendToFlow(
            IOrganizationService systemService,
            ITracingService tracingService,
            IPluginExecutionContext context,
            string displayName,
            string requestFingerprint,
            InitiatingUserDetails initiatingUser,
            bool shouldBlock,
            string blockReason,
            DateTime attemptedOnUtc)
        {
            var flowUrl = GetEnvironmentVariableValue(systemService, FlowUrlEnvironmentVariableSchemaName, tracingService);
            if (string.IsNullOrWhiteSpace(flowUrl))
            {
                tracingService?.Trace(
                    "LogAgentCreationAttemptPlugin: FlowUrl not configured. Create Environment Variable with schema name {0}.",
                    FlowUrlEnvironmentVariableSchemaName);
                return;
            }

            try
            {
                var payload = new FlowLogPayload
                {
                    AttemptedOnUtc = attemptedOnUtc.ToString("o"),   // ISO-8601 round-trip
                    AgentDisplayName = displayName,
                    RequestFingerprint = requestFingerprint,
                    MessageName = context.MessageName,
                    PrimaryEntity = context.PrimaryEntityName,
                    Stage = context.Stage,
                    IsInTransaction = context.IsInTransaction,
                    CorrelationId = context.CorrelationId.ToString(),
                    RequestId = context.RequestId.HasValue ? context.RequestId.Value.ToString() : Guid.Empty.ToString(),
                    OrganizationId = context.OrganizationId.ToString(),
                    InitiatingUserId = context.InitiatingUserId.ToString(),
                    InitiatingUserInternalEmailAddress = initiatingUser?.InternalEmailAddress,
                    InitiatingUserFullName = initiatingUser?.FullName,
                    InitiatingUserDomainName = initiatingUser?.DomainName,
                    InitiatingUserAccessMode = initiatingUser?.AccessMode,
                    InitiatingUserEntraObjectId = initiatingUser?.EntraObjectId,
                    UserId = context.UserId.ToString(),
                    ShouldBlock = shouldBlock,
                    BlockReason = blockReason
                };

                var body = SerializePayload(payload);

                using (var request = new HttpRequestMessage(HttpMethod.Post, flowUrl))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    // Plug-ins run synchronously in the pipeline — no async infrastructure here,
                    // so block on the call. FlowHttpClient.Timeout enforces the deadline; a slow
                    // Flow surfaces as TaskCanceledException, caught below.
                    var response = FlowHttpClient.SendAsync(request).GetAwaiter().GetResult();
                    var responseBody = ReadResponseBodySafe(response);

                    tracingService?.Trace(
                        "LogAgentCreationAttemptPlugin: Flow call status={0}, reason={1}, response={2}",
                        (int)response.StatusCode,
                        response.ReasonPhrase,
                        responseBody);
                }
            }
            catch (Exception ex)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: Flow call failed. Error={0}", ex);
            }
        }

        // Retrieves the initiating user's systemuser record via the privileged service so it
        // always succeeds regardless of the calling user's row-level security. Returns an empty
        // object (never null) on any failure — which fails closed at the allow-list check.
        private static InitiatingUserDetails GetInitiatingUserDetails(
            IOrganizationService systemService,
            IPluginExecutionContext context,
            ITracingService tracingService)
        {
            if (systemService == null || context.InitiatingUserId == Guid.Empty)
            {
                return new InitiatingUserDetails();
            }

            try
            {
                var user = systemService.Retrieve(
                    "systemuser",
                    context.InitiatingUserId,
                    new ColumnSet("internalemailaddress", "fullname", "domainname", "accessmode", "azureactivedirectoryobjectid"));

                return new InitiatingUserDetails
                {
                    InternalEmailAddress = user.GetAttributeValue<string>("internalemailaddress"),
                    FullName = user.GetAttributeValue<string>("fullname"),
                    DomainName = user.GetAttributeValue<string>("domainname"),
                    AccessMode = user.GetAttributeValue<OptionSetValue>("accessmode")?.Value,
                    EntraObjectId = user.GetAttributeValue<Guid?>("azureactivedirectoryobjectid")?.ToString()
                };
            }
            catch (Exception ex)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: Failed to load initiating user details. Error={0}", ex);
                return new InitiatingUserDetails();
            }
        }

        // Reads a Dataverse Environment Variable value by schema name. The environment-specific
        // override (environmentvariablevalue) takes precedence over the definition's default
        // (environmentvariabledefinition), matching how Dataverse resolves them at runtime.
        // Returns null when no matching definition exists or on any failure (fail-closed).
        private static string GetEnvironmentVariableValue(IOrganizationService service, string schemaName, ITracingService tracingService)
        {
            if (service == null)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: systemService unavailable for environment variable lookup of {0}.", schemaName);
                return null;
            }

            try
            {
                var query = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("defaultvalue"),
                    TopCount = 1    // Schema names are unique.
                };

                query.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);

                // Left outer join so the definition row returns even when no override exists.
                var valueLink = query.AddLink(
                    "environmentvariablevalue",
                    "environmentvariabledefinitionid",
                    "environmentvariabledefinitionid",
                    JoinOperator.LeftOuter);
                valueLink.EntityAlias = "evv";
                valueLink.Columns = new ColumnSet("value");

                var result = service.RetrieveMultiple(query);

                string value = null;
                if (result?.Entities != null && result.Entities.Count > 0)
                {
                    var definition = result.Entities[0];
                    value = GetAliasedString(definition, "evv.value");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        value = definition.GetAttributeValue<string>("defaultvalue");
                    }
                }

                tracingService?.Trace(
                    "LogAgentCreationAttemptPlugin: Environment variable lookup for {0}. ValueFound={1}",
                    schemaName,
                    !string.IsNullOrWhiteSpace(value));

                return value;
            }
            catch (Exception ex)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: Environment variable lookup for {0} failed. Error={1}", schemaName, ex);
                return null;
            }
        }

        // Safely extracts a string from an AliasedValue (present when a joined entity's column
        // is returned). Returns null if absent or not a string.
        private static string GetAliasedString(Entity entity, string alias)
        {
            if (entity == null || string.IsNullOrWhiteSpace(alias) || !entity.Contains(alias))
            {
                return null;
            }

            var aliased = entity.GetAttributeValue<AliasedValue>(alias);
            return aliased?.Value as string;
        }

        // Serializes the payload to UTF-8 JSON using the built-in DataContractJsonSerializer
        // (no external NuGet dependency — important in the Dataverse sandbox).
        private static string SerializePayload(FlowLogPayload payload)
        {
            var serializer = new DataContractJsonSerializer(typeof(FlowLogPayload));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, payload);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        // Reads the HTTP response body without throwing. Flattens newlines and truncates to 400
        // chars so trace log entries stay within size limits.
        private static string ReadResponseBodySafe(HttpResponseMessage response)
        {
            if (response == null || response.Content == null)
            {
                return string.Empty;
            }

            try
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(body))
                {
                    return string.Empty;
                }

                var normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();
                if (normalized.Length > 400)
                {
                    return normalized.Substring(0, 400);
                }

                return normalized;
            }
            catch
            {
                return "(response body unavailable)";
            }
        }

        // Parses the comma-separated allow-list into trimmed, non-empty substrings. Strips outer
        // and per-token quotes an admin may have pasted in.
        private static List<string> ParseContainsAllowList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            raw = raw.Trim();

            // Strip outer double-quotes wrapping the whole value.
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                raw = raw.Substring(1, raw.Length - 2);
            }

            return raw.Split(',')
                .Select(p => p.Trim().Trim('"', '\''))
                .Where(p => p.Length > 0)
                .ToList();
        }

        // True if the email contains at least one allow-list substring (case-insensitive). An
        // empty allow-list always returns false (intentional full-block mode).
        private static bool EmailMatchesContainsAllowList(string email, IList<string> allowList)
        {
            return !string.IsNullOrWhiteSpace(email)
                && allowList.Any(c => email.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Produces a stable request identifier to correlate the trace log with the Flow audit
        // record. Preference: CorrelationId -> RequestId -> a new GUID (last resort).
        private static string GetRequestFingerprint(IPluginExecutionContext context)
        {
            if (context.CorrelationId != Guid.Empty)
            {
                return context.CorrelationId.ToString();
            }

            if (context.RequestId.HasValue && context.RequestId.Value != Guid.Empty)
            {
                return context.RequestId.Value.ToString();
            }

            return Guid.NewGuid().ToString();
        }

        // Reads a human-readable name from the candidate columns in order; first non-blank wins.
        // Returns null when none are present (the caller substitutes a placeholder).
        private static string ResolveDisplayName(Entity target)
        {
            foreach (var column in DisplayNameCandidateColumns)
            {
                if (target.Contains(column))
                {
                    var value = target.GetAttributeValue<string>(column);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }
}
