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
    //   2. Resolves the agent's harness (Standard, Copilot Chat, GitHub Copilot, Customer
    //      Conversation or Unknown) from the "template" column.
    //   3. Allows the creation if EITHER the email matches the allow-list in the Environment
    //      Variable "ptricks_AgentCreationAllowedEmailContains", OR the harness is listed in
    //      "ptricks_AgentCreationAllowedHarnesses". Otherwise it blocks, and labels the block:
    //        - FullBlock:    no harness at all is allowed in this environment;
    //        - PartialBlock: another harness is allowed, so the user can be pointed to it.
    //   4. POSTs an audit payload to a Power Automate Flow (URL from the Environment Variable
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

        // A harness the plug-in knows about: the stable key used in configuration and in the
        // notification card settings, plus the name shown to makers.
        private sealed class HarnessDefinition
        {
            public HarnessDefinition(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }

            public string Key { get; private set; }
            public string DisplayName { get; private set; }
        }

        // Maps a "template" column prefix to a harness key.
        private sealed class TemplatePrefixRule
        {
            public TemplatePrefixRule(string prefix, string harnessKey)
            {
                Prefix = prefix;
                HarnessKey = harnessKey;
            }

            public string Prefix { get; private set; }
            public string HarnessKey { get; private set; }
        }

        // Optional JSON shape of the BlockMessage Environment Variable: one message per block
        // scope. A plain-text value is also accepted and used for both scopes.
        [DataContract]
        private sealed class ScopedBlockMessages
        {
            [DataMember(Name = "FullBlock")]
            public string FullBlock { get; set; }

            [DataMember(Name = "PartialBlock")]
            public string PartialBlock { get; set; }
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

            // --- Harness ---

            // Raw "template" column value as received (null when not supplied).
            [DataMember(Name = "agentTemplate")]
            public string AgentTemplate { get; set; }

            // Harness key, e.g. "GitHubCopilot".
            [DataMember(Name = "harness")]
            public string Harness { get; set; }

            // Harness display name, e.g. "GitHub Copilot".
            [DataMember(Name = "harnessDisplayName")]
            public string HarnessDisplayName { get; set; }

            // "FullBlock", "PartialBlock" or "None" (not blocked).
            [DataMember(Name = "blockScope")]
            public string BlockScope { get; set; }

            // Display names of the harnesses allowed in this environment at the time of the
            // attempt, joined in natural language (e.g. "Standard or Copilot Chat"). Empty when
            // none is allowed.
            [DataMember(Name = "allowedHarnesses")]
            public string AllowedHarnesses { get; set; }
        }

        // Dataverse logical name for a Copilot Studio agent.
        private const string BotEntityLogicalName = "bot";

        // Fallback message surfaced to the user when a creation attempt is blocked and no
        // custom message is configured via the BlockMessage Environment Variable.
        private const string BlockMessage = "Agent creation is not allowed in this environment and has been blocked by a Dataverse plugin. Please contact your administrator for more information.";

        // Environment Variable schema names. The values they resolve to are admin-editable
        // without redeploying this assembly. All share the prefix below, so they are read in a
        // single query.
        private const string EnvironmentVariablePrefix = "ptricks_AgentCreation";
        private const string FlowUrlEnvironmentVariableSchemaName = "ptricks_AgentCreationLoggingFlowUrl";
        private const string AllowedEmailContainsEnvironmentVariableSchemaName = "ptricks_AgentCreationAllowedEmailContains";
        private const string BlockMessageEnvironmentVariableSchemaName = "ptricks_AgentCreationBlockMessage";

        // Comma-separated list of harness keys anyone may create (e.g. "Standard,CopilotChat").
        // Blank or unreadable means no harness is allowed (fail-closed full-block mode).
        private const string AllowedHarnessesEnvironmentVariableSchemaName = "ptricks_AgentCreationAllowedHarnesses";

        // The "bot" column the harness is resolved from.
        private const string BotTemplateColumnName = "template";

        // Harness keys. These exact strings are used in the AllowedHarnesses Environment Variable
        // and as keys in the notification card settings, so they must stay stable.
        private const string HarnessStandard = "Standard";
        private const string HarnessCopilotChat = "CopilotChat";
        private const string HarnessGitHubCopilot = "GitHubCopilot";
        private const string HarnessCustomerConversation = "CustomerConversation";
        private const string HarnessUnknown = "Unknown";

        // Block scope values, sent to the Flow and used to choose the message.
        private const string BlockScopeFull = "FullBlock";
        private const string BlockScopePartial = "PartialBlock";
        private const string BlockScopeNone = "None";

        // Tokens replaced in the block message.
        private const string HarnessNameToken = "{HarnessName}";
        private const string AllowedHarnessesToken = "{AllowedHarnesses}";

        // Known harnesses, in display order (used for parsing the allowed list and for listing
        // alternatives in messages).
        private static readonly HarnessDefinition[] Harnesses =
        {
            new HarnessDefinition(HarnessStandard, "Standard"),
            new HarnessDefinition(HarnessCopilotChat, "Copilot Chat"),
            new HarnessDefinition(HarnessGitHubCopilot, "GitHub Copilot"),
            new HarnessDefinition(HarnessCustomerConversation, "Customer Conversation"),
            new HarnessDefinition(HarnessUnknown, "Unknown")
        };

        // Template prefix rules, checked in order; first match wins (case-insensitive). These
        // mirror the harness classification used in reporting. A null/absent template is
        // Standard; anything unmatched (including an empty value) is Unknown.
        private static readonly TemplatePrefixRule[] TemplatePrefixRules =
        {
            new TemplatePrefixRule("default", HarnessStandard),
            new TemplatePrefixRule("powerpages-1", HarnessStandard),
            new TemplatePrefixRule("citizen", HarnessStandard),
            new TemplatePrefixRule("system", HarnessStandard),
            new TemplatePrefixRule("o365LicenseRestrictedAgent", HarnessStandard),
            new TemplatePrefixRule("ivr-", HarnessStandard),
            new TemplatePrefixRule("gpt", HarnessCopilotChat),
            new TemplatePrefixRule("cli", HarnessGitHubCopilot),
            new TemplatePrefixRule("conversationalai", HarnessCustomerConversation)
        };

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

            // All of this plug-in's Environment Variables, read in one query.
            var environmentVariables = GetEnvironmentVariableValues(systemService, tracingService);

            // Comma-separated allow-list, e.g. "@contoso.com,@fabrikam.com". Allowed if the email
            // contains ANY allow-list substring. An empty or unreadable allow-list means nobody
            // is permitted by email.
            var allowedEmailContainsValues = ParseCommaSeparatedValues(
                GetValue(environmentVariables, AllowedEmailContainsEnvironmentVariableSchemaName));
            var isAllowedByEmailContains = EmailMatchesContainsAllowList(initiatingUser?.InternalEmailAddress, allowedEmailContainsValues);

            // Harness of the agent being created, and the harnesses allowed in this environment.
            string agentTemplate;
            var harness = ResolveHarness(target, out agentTemplate);
            var allowedHarnesses = ParseAllowedHarnesses(
                GetValue(environmentVariables, AllowedHarnessesEnvironmentVariableSchemaName),
                tracingService);
            var isAllowedByHarness = allowedHarnesses.Contains(harness.Key);

            // Alternatives the user can be pointed to. "Unknown" is never offered as one.
            var alternatives = Harnesses
                .Where(h => h.Key != HarnessUnknown && allowedHarnesses.Contains(h.Key))
                .ToList();
            var allowedHarnessesDisplay = JoinNatural(alternatives.Select(h => h.DisplayName).ToList());

            var shouldBlock = !isAllowedByEmailContains && !isAllowedByHarness;
            var blockScope = !shouldBlock
                ? BlockScopeNone
                : (alternatives.Count == 0 ? BlockScopeFull : BlockScopePartial);

            var blockReason = BuildBlockReason(
                isAllowedByEmailContains,
                harness,
                agentTemplate,
                allowedHarnesses,
                blockScope);

            tracingService?.Trace(
                "LogAgentCreationAttemptPlugin: Attempt evaluated. Email={0}, AllowListCount={1}, IsAllowedByContains={2}, Template={3}, Harness={4}, AllowedHarnesses={5}, IsAllowedByHarness={6}, ShouldBlock={7}, BlockScope={8}",
                initiatingUser?.InternalEmailAddress,
                allowedEmailContainsValues.Count,
                isAllowedByEmailContains,
                agentTemplate ?? "(null)",
                harness.Key,
                string.Join(",", allowedHarnesses),
                isAllowedByHarness,
                shouldBlock,
                blockScope);

            // May be blank at PreValidation before Dataverse fills default fields.
            var displayName = ResolveDisplayName(target) ?? "(display name not provided)";

            // Log BEFORE throwing the block: a transaction rollback would discard Dataverse-based
            // logging, but this outbound HTTP call survives it. A failed Flow call is non-fatal —
            // the block decision is independent of logging.
            TrySendToFlow(
                GetValue(environmentVariables, FlowUrlEnvironmentVariableSchemaName),
                tracingService,
                context,
                new FlowLogPayload
                {
                    AttemptedOnUtc = attemptedOnUtc.ToString("o"),   // ISO-8601 round-trip
                    AgentDisplayName = displayName,
                    RequestFingerprint = requestFingerprint,
                    InitiatingUserInternalEmailAddress = initiatingUser?.InternalEmailAddress,
                    InitiatingUserFullName = initiatingUser?.FullName,
                    InitiatingUserDomainName = initiatingUser?.DomainName,
                    InitiatingUserAccessMode = initiatingUser?.AccessMode,
                    InitiatingUserEntraObjectId = initiatingUser?.EntraObjectId,
                    ShouldBlock = shouldBlock,
                    BlockReason = blockReason,
                    AgentTemplate = agentTemplate,
                    Harness = harness.Key,
                    HarnessDisplayName = harness.DisplayName,
                    BlockScope = blockScope,
                    AllowedHarnesses = allowedHarnessesDisplay
                });

            if (shouldBlock)
            {
                // Admin-customizable message via Environment Variable; falls back to the hardcoded
                // default when unset or unreadable (fail-safe — a block always has a message).
                var blockMessage = ResolveBlockMessage(
                    GetValue(environmentVariables, BlockMessageEnvironmentVariableSchemaName),
                    blockScope,
                    harness.DisplayName,
                    allowedHarnessesDisplay,
                    tracingService);

                tracingService?.Trace("LogAgentCreationAttemptPlugin: Throwing block exception. BlockScope={0}", blockScope);

                // Cancels the Dataverse transaction and surfaces the friendly message. The audit
                // call above already fired, so the log entry exists regardless.
                throw new InvalidPluginExecutionException(blockMessage);
            }

            tracingService?.Trace(
                "LogAgentCreationAttemptPlugin: Completed — not blocking (allowed by email: {0}, allowed by harness: {1}).",
                isAllowedByEmailContains,
                isAllowedByHarness);
        }

        // Resolves the harness from the Target's "template" column. A null or absent template is
        // Standard; otherwise the trimmed value is matched against the prefix rules; anything
        // unmatched (including an empty value) is Unknown. The raw template is returned for
        // auditing.
        private static HarnessDefinition ResolveHarness(Entity target, out string rawTemplate)
        {
            rawTemplate = target.Contains(BotTemplateColumnName)
                ? target.GetAttributeValue<string>(BotTemplateColumnName)
                : null;

            if (rawTemplate == null)
            {
                return GetHarness(HarnessStandard);
            }

            var template = rawTemplate.Trim();
            foreach (var rule in TemplatePrefixRules)
            {
                if (template.StartsWith(rule.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return GetHarness(rule.HarnessKey);
                }
            }

            return GetHarness(HarnessUnknown);
        }

        private static HarnessDefinition GetHarness(string key)
        {
            return Harnesses.First(h => h.Key == key);
        }

        // Parses the AllowedHarnesses value into a set of canonical harness keys. Entries are
        // matched case-insensitively and ignoring spaces, so "Copilot Chat" and "copilotchat"
        // both match "CopilotChat". Unrecognised entries are ignored and traced. Blank or
        // unreadable means an empty set (no harness allowed).
        private static HashSet<string> ParseAllowedHarnesses(string raw, ITracingService tracingService)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in ParseCommaSeparatedValues(raw))
            {
                var normalized = entry.Replace(" ", string.Empty);
                var match = Harnesses.FirstOrDefault(h => string.Equals(h.Key, normalized, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    result.Add(match.Key);
                }
                else
                {
                    tracingService?.Trace(
                        "LogAgentCreationAttemptPlugin: Ignoring unrecognised entry \"{0}\" in {1}.",
                        entry,
                        AllowedHarnessesEnvironmentVariableSchemaName);
                }
            }

            return result;
        }

        // Joins names in natural language: "A", "A or B", "A, B or C". Empty for no names.
        private static string JoinNatural(IList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }

            if (names.Count == 1)
            {
                return names[0];
            }

            return string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1];
        }

        // Plain-language explanation of the decision, written to the audit record.
        private static string BuildBlockReason(
            bool isAllowedByEmailContains,
            HarnessDefinition harness,
            string agentTemplate,
            ICollection<string> allowedHarnesses,
            string blockScope)
        {
            var templateText = agentTemplate == null ? "no template" : "template '" + agentTemplate + "'";
            var allowedText = allowedHarnesses.Count == 0 ? "none" : string.Join(", ", allowedHarnesses);

            if (isAllowedByEmailContains)
            {
                return "Initiating user email matched a configured allow-list value; harness "
                    + harness.Key + " (" + templateText + ") not checked. Not blocked.";
            }

            if (blockScope == BlockScopeNone)
            {
                return "Initiating user email did not match the allow-list, but harness "
                    + harness.Key + " (" + templateText + ") is in the allowed harnesses ("
                    + allowedText + "). Not blocked.";
            }

            return "Initiating user email did not match the allow-list and harness "
                + harness.Key + " (" + templateText + ") is not in the allowed harnesses ("
                + allowedText + "). " + blockScope + ".";
        }

        // Chooses the in-product block message. The Environment Variable may hold plain text
        // (used for every scope) or JSON with FullBlock / PartialBlock keys. A missing or blank
        // key falls back to FullBlock, then to the hardcoded default. The {HarnessName} and
        // {AllowedHarnesses} tokens are replaced in the chosen text.
        private static string ResolveBlockMessage(
            string raw,
            string blockScope,
            string harnessDisplayName,
            string allowedHarnessesDisplay,
            ITracingService tracingService)
        {
            string message = null;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                var trimmed = raw.Trim();
                var scoped = trimmed.StartsWith("{", StringComparison.Ordinal)
                    ? TryDeserializeScopedBlockMessages(trimmed, tracingService)
                    : null;

                if (scoped != null)
                {
                    message = blockScope == BlockScopePartial && !string.IsNullOrWhiteSpace(scoped.PartialBlock)
                        ? scoped.PartialBlock
                        : scoped.FullBlock;
                }
                else
                {
                    message = trimmed;
                }
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = BlockMessage;
            }

            return message
                .Replace(HarnessNameToken, harnessDisplayName ?? string.Empty)
                .Replace(AllowedHarnessesToken, allowedHarnessesDisplay ?? string.Empty);
        }

        // Returns null when the value is not valid JSON of the expected shape, so the caller
        // treats it as plain text instead.
        private static ScopedBlockMessages TryDeserializeScopedBlockMessages(string json, ITracingService tracingService)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ScopedBlockMessages));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as ScopedBlockMessages;
                }
            }
            catch (Exception ex)
            {
                tracingService?.Trace(
                    "LogAgentCreationAttemptPlugin: {0} looks like JSON but could not be parsed; using it as plain text. Error={1}",
                    BlockMessageEnvironmentVariableSchemaName,
                    ex.Message);
                return null;
            }
        }

        // Completes the payload with the pipeline context fields and POSTs it to the Flow. Never
        // throws — it runs before the optional block exception, so all failures (including
        // timeouts) are caught and traced.
        private static void TrySendToFlow(
            string flowUrl,
            ITracingService tracingService,
            IPluginExecutionContext context,
            FlowLogPayload payload)
        {
            if (string.IsNullOrWhiteSpace(flowUrl))
            {
                tracingService?.Trace(
                    "LogAgentCreationAttemptPlugin: FlowUrl not configured. Create Environment Variable with schema name {0}.",
                    FlowUrlEnvironmentVariableSchemaName);
                return;
            }

            try
            {
                payload.MessageName = context.MessageName;
                payload.PrimaryEntity = context.PrimaryEntityName;
                payload.Stage = context.Stage;
                payload.IsInTransaction = context.IsInTransaction;
                payload.CorrelationId = context.CorrelationId.ToString();
                payload.RequestId = context.RequestId.HasValue ? context.RequestId.Value.ToString() : Guid.Empty.ToString();
                payload.OrganizationId = context.OrganizationId.ToString();
                payload.InitiatingUserId = context.InitiatingUserId.ToString();
                payload.UserId = context.UserId.ToString();

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

        // Reads every Environment Variable whose schema name starts with the plug-in's prefix, in
        // a single query. The environment-specific override (environmentvariablevalue) takes
        // precedence over the definition's default (environmentvariabledefinition), matching how
        // Dataverse resolves them at runtime. Returns an empty dictionary on any failure, so every
        // lookup resolves to null (fail-closed).
        private static Dictionary<string, string> GetEnvironmentVariableValues(IOrganizationService service, ITracingService tracingService)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (service == null)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: systemService unavailable for environment variable lookup.");
                return values;
            }

            try
            {
                var query = new QueryExpression("environmentvariabledefinition")
                {
                    ColumnSet = new ColumnSet("schemaname", "defaultvalue")
                };

                query.Criteria.AddCondition("schemaname", ConditionOperator.BeginsWith, EnvironmentVariablePrefix);

                // Left outer join so the definition row returns even when no override exists.
                var valueLink = query.AddLink(
                    "environmentvariablevalue",
                    "environmentvariabledefinitionid",
                    "environmentvariabledefinitionid",
                    JoinOperator.LeftOuter);
                valueLink.EntityAlias = "evv";
                valueLink.Columns = new ColumnSet("value");

                var result = service.RetrieveMultiple(query);

                foreach (var definition in result?.Entities ?? Enumerable.Empty<Entity>())
                {
                    var schemaName = definition.GetAttributeValue<string>("schemaname");
                    if (string.IsNullOrWhiteSpace(schemaName))
                    {
                        continue;
                    }

                    var value = GetAliasedString(definition, "evv.value");
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        value = definition.GetAttributeValue<string>("defaultvalue");
                    }

                    // A definition can come back on more than one row; keep the first non-blank value.
                    string existing;
                    if (!values.TryGetValue(schemaName, out existing) || string.IsNullOrWhiteSpace(existing))
                    {
                        values[schemaName] = value;
                    }
                }

                tracingService?.Trace(
                    "LogAgentCreationAttemptPlugin: Environment variables found: {0}",
                    string.Join(", ", values.Select(v => v.Key + (string.IsNullOrWhiteSpace(v.Value) ? " (blank)" : string.Empty))));
            }
            catch (Exception ex)
            {
                tracingService?.Trace("LogAgentCreationAttemptPlugin: Environment variable lookup failed. Error={0}", ex);
                values.Clear();
            }

            return values;
        }

        private static string GetValue(IDictionary<string, string> values, string schemaName)
        {
            string value;
            return values.TryGetValue(schemaName, out value) ? value : null;
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

        // Parses a comma-separated value into trimmed, non-empty entries. Strips outer and
        // per-entry quotes an admin may have pasted in.
        private static List<string> ParseCommaSeparatedValues(string raw)
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
        // empty allow-list always returns false.
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
