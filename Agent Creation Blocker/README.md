# Agent Creation Blocker

The **Agent Creation Blocker** blocks Copilot Studio agent creation per environment, and optionally per harness, which the native Power Platform controls cannot cleanly do. It intercepts the creation at the Dataverse layer with a plugin, logs every attempt in a central Dataverse table, and notifies the blocked maker with a configurable Teams Adaptive Card.

This README is the **technical setup reference**. For the rationale, when to use it, the maker experience, and how the pattern extends further, see the blog post: [Blocking Copilot Studio Agent Creation per Environment and Harness](https://www.powertricks.io/block-agent-creation)

![Architecture: a satellite solution in each blocked environment intercepts agent creation and calls a core solution in a central environment for centralized logging and Teams notification](Screenshots/block-agent-creation-arch.webp)

**Current version: `2026.11.23.0`** (Core and Satellite). Upgrading from `2026.09.04.1`? Follow [Upgrading from 2026.09.04.1](#upgrading-from-2026090041): the upgrade order matters.

## How it works
1. A maker tries to create an agent; Copilot Studio writes a record into the `bot` table.
2. The **satellite** plugin runs on `Create` of `bot` (PreValidation stage, synchronous). It resolves the agent's harness from `bot.template` (see [Harness resolution](#harness-resolution)) and allows the creation if **either** the maker's email matches the allow-list **or** the harness is in the allowed harnesses list. Otherwise the creation is blocked, as a:
   - **Full block** when no harness is allowed in the environment, or
   - **Partial block** when another harness is allowed, so the maker can be pointed to it.
3. The plugin calls the **core** logging flow (HTTP POST) to record the attempt, whether it is blocked or not.
4. If the creation is blocked, the plugin throws an `InvalidPluginExecutionException`, so the `bot` write is rolled back and creation fails.
5. For blocked attempts, the core notification flow posts the matching Teams Adaptive Card (full block or partial block) to the maker and marks the attempt as notified.

> **Note:** This relies on current platform behavior: Copilot Studio writing the new agent into the `bot` table at creation time, with a `template` value identifying the harness. That behavior is not officially documented, so a future platform change could stop the interception from firing. If that happens the block simply stops taking effect; uninstalling the satellite solution removes the control entirely, with no residual effect on agent creation.

## Solution components
The control ships as two solutions. Both are provided as Managed and Unmanaged packages.

### `BlockAgentCreationCore`: installed once, in a central environment
| Component | Logical name | Purpose |
|---|---|---|
| Table | `ptricks_agentcreationattempt` | Central log of every attempt (see [columns](#the-attempt-table)) |
| Model-driven app | `Agent Creation Attempt` | Review logged attempts |
| Cloud flow | `bac - Log Agent Creation Attempt` | HTTP-triggered; called by the satellite plugin, writes the attempt record |
| Cloud flow | `bac - Notify Maker After Block` | Dataverse-triggered; posts the Teams Adaptive Card matching the block scope, then marks the attempt as notified |
| Environment variable | `ptricks_AgentCreationCardSettings` | JSON driving the Teams card, with a `FullBlock` and a `PartialBlock` card (see [Notification cards](#notification-cards)) |
| Connection references | Microsoft Dataverse, Office 365 Users, Power Platform for Admins, Microsoft Teams | Used by the two core flows |

### `BlockAgentCreationSat`: installed in every blocked environment
| Component | Logical name | Purpose |
|---|---|---|
| Plugin assembly | `BlockAndLogAgentCreation.dll` | Contains the blocking plugin (source: [`pluginSource.cs`](pluginSource.cs)) |
| Plugin step | `Create of bot` (PreValidation) | Intercepts agent creation |
| Cloud flow | `bac - Auto Turn On Plugin Step` | Self-healing: re-enables the plugin step if it is disabled |
| Environment variable | `ptricks_AgentCreationLoggingFlowUrl` | Core logging-flow endpoint to POST attempts to |
| Environment variable | `ptricks_AgentCreationBlockMessage` | Exception message thrown when creation is blocked (plain text, or JSON per block scope) |
| Environment variable | `ptricks_AgentCreationAllowedEmailContains` | Comma-separated allow-list of email patterns |
| Environment variable | `ptricks_AgentCreationAllowedHarnesses` | Comma-separated list of harnesses anyone may create in the environment |
| Connection reference | Microsoft Dataverse | Used by the self-healing flow |

## Pre-requisites
- Dataverse-enabled environments (one central + each environment to block).
- An account with the Power Platform Administrator role to import the solutions and create the connections.
- Connections available in the central environment for: Microsoft Dataverse, Office 365 Users, Power Platform for Admins, and Microsoft Teams. In each blocked environment: Microsoft Dataverse.
- A premium license for the account owning the flows.

## Setup
Set up the core once, configure the satellite in that same central environment, then export the configured satellite and distribute it to each blocked environment.

> Already running `2026.09.04.1`? Do not follow these steps; follow [Upgrading from 2026.09.04.1](#upgrading-from-2026090041) instead.

### 1. Install and configure the core solution
1. Import `BlockAgentCreationCore` into the central environment and set the connection references when prompted.
2. Set `ptricks_AgentCreationCardSettings` to the cards to send to blocked makers (see [Notification cards](#notification-cards)). The shipped value is a working example to adapt.
3. Turn on both flows (`bac - Log Agent Creation Attempt`, `bac - Notify Maker After Block`).
4. Open `bac - Log Agent Creation Attempt` and copy the URL of its **When an HTTP request is received** trigger: the satellite needs it.

### 2. Install and configure the satellite solution (same central environment)
Install the satellite in the central environment first so it can be configured once, then exported ready to deploy.
1. Import `BlockAgentCreationSat` and set its Dataverse connection reference.
2. Set the satellite environment variables:
   - `ptricks_AgentCreationLoggingFlowUrl` → the logging-flow URL copied above.
   - `ptricks_AgentCreationBlockMessage` → the exception message to throw (see [Block message](#block-message)).
   - `ptricks_AgentCreationAllowedEmailContains` → the allow-list (comma-separated email patterns; leave empty to allow no maker by email).
   - `ptricks_AgentCreationAllowedHarnesses` → the harnesses anyone may create (see [Allowed harnesses](#allowed-harnesses)); leave empty to block every harness.
3. Turn on the `bac - Auto Turn On Plugin Step` flow.

### 3. Distribute to each blocked environment
1. Export the configured satellite solution.
2. Import it into every environment where agent creation should be blocked, setting the Dataverse connection and confirming or overriding the environment variables per environment.
3. Turn on the `bac - Auto Turn On Plugin Step` flow in each.
4. For repeatable rollout at scale, pair it with the [Solution Deployer](https://www.powertricks.io/solution-deployer): no pipeline or Managed Environments required.

Because the exception message, the allow-list, and the allowed harnesses are environment variables, each blocked environment can be tuned independently without touching the others or the plugin code.

## Configuration
All per-environment behavior is exposed through environment variables, with no plugin code changes required.

| Environment variable | Solution | Controls |
|---|---|---|
| `ptricks_AgentCreationCardSettings` | Core | JSON for the Teams cards sent after a full block and a partial block |
| `ptricks_AgentCreationLoggingFlowUrl` | Satellite | Core logging-flow endpoint each satellite POSTs to |
| `ptricks_AgentCreationBlockMessage` | Satellite | Exception message thrown when creation is blocked: plain text, or JSON with a `FullBlock` and a `PartialBlock` message |
| `ptricks_AgentCreationAllowedEmailContains` | Satellite | Comma-separated email patterns; a match allows creation on any harness (still logged) |
| `ptricks_AgentCreationAllowedHarnesses` | Satellite | Comma-separated harnesses anyone may create; blank blocks every harness |

### Harness resolution
The plugin maps the `template` column of the new `bot` record to a harness. Rules are checked in order on the trimmed value, prefix matching is case-insensitive, and the first match wins.

| `template` value | Harness key | Display name |
|---|---|---|
| Starts with `default`, `powerpages-1`, `citizen`, `system`, `o365LicenseRestrictedAgent` or `ivr-` | `Standard` | Standard |
| Starts with `gpt` | `CopilotChat` | Copilot Chat |
| Starts with `cli` | `GitHubCopilot` | GitHub Copilot |
| Starts with `conversationalai` | `CustomerConversation` | Customer Conversation |
| Missing (null, or not in the create request) | `Standard` | Standard |
| Anything else, including an empty value | `Unknown` | Unknown |

A harness Microsoft adds in the future resolves to `Unknown` and stays blocked until the mapping in the plugin is extended (fail-closed).

> A missing `template` resolves to `Standard`, consistent with how older agents appear in reporting. This assumes every newer harness always sets `template` at creation time. The `ptricks_agenttemplate` column of the attempt table makes it visible if one ever does not.

### Allowed harnesses
`ptricks_AgentCreationAllowedHarnesses` is a comma-separated list of harness keys: `Standard`, `CopilotChat`, `GitHubCopilot`, `CustomerConversation`, `Unknown`.

- Entries are trimmed, quotes are stripped, and matching ignores case and spaces (`Copilot Chat` and `copilotchat` both match `CopilotChat`).
- Unrecognized entries are ignored and written to the plugin trace log.
- A blank or unreadable value means no harness is allowed, so every maker not on the email allow-list gets a **full block**.
- `Unknown` can be listed to deliberately allow unrecognized templates, but it is never offered to the maker as an alternative, so a list containing only `Unknown` still produces full blocks.

The allow-list runs alongside it: a maker whose email matches `ptricks_AgentCreationAllowedEmailContains` is always allowed, on any harness, and logged.

| Goal | `ptricks_AgentCreationAllowedHarnesses` | Result for a maker not on the email allow-list |
|---|---|---|
| Block every agent creation (default) | *(blank)* | Every harness blocked, full block |
| Allow only the traditional harnesses | `Standard,CopilotChat` | GitHub Copilot, Customer Conversation and Unknown blocked with a partial block pointing to "Standard or Copilot Chat" |
| Allow only Standard | `Standard` | Everything else blocked with a partial block pointing to "Standard" |
| Block only GitHub Copilot (same as the previous version's harness-only mode) | `Standard,CopilotChat,CustomerConversation` | GitHub Copilot and Unknown blocked with a partial block |
| Allow everything | `Standard,CopilotChat,GitHubCopilot,CustomerConversation,Unknown` | Nothing blocked |

### Block message
`ptricks_AgentCreationBlockMessage` is the message of the exception thrown when creation is blocked. Copilot Studio may or may not surface it, depending on current portal behavior, which is why the Teams card exists. The value can be either:

- **plain text**, used for every block, or
- **a JSON object** with a message per block scope. `PartialBlock` supports the `{HarnessName}` and `{AllowedHarnesses}` tokens. If the key for the current scope is missing or blank, `FullBlock` is used, then a built-in default.

```json
{
  "FullBlock": "Copilot Studio agent creation is not supported in this environment. Contact your administrator for more information.",
  "PartialBlock": "{HarnessName} agents are not supported in this environment. You can still create a {AllowedHarnesses} agent instead. Contact your administrator for more information."
}
```

### Notification cards
`ptricks_AgentCreationCardSettings` holds two cards, and the notify flow picks one from the `ptricks_blockscope` column of the attempt record:

| Key | Sent when | Typical message |
|---|---|---|
| `FullBlock` | No harness is allowed in the environment | Agent creation is not allowed here; go to an approved environment |
| `PartialBlock` | Another harness is allowed | This harness is not allowed here; create the agent on an allowed harness instead |

Each card holds `CardImageLink`, `TitleText`, `IntroText`, `NextStepsText` and `Links`. Each `Links` entry has a `Title`, a `Url`, and an optional `Style` (e.g. `positive`, `destructive`; omit for the default). **Any field missing from `PartialBlock` is taken from `FullBlock`**, so the image and links only need to be written once.

`TitleText`, `IntroText` and `NextStepsText` support these tokens, substituted at runtime:

| Token | Replaced with |
|---|---|
| `{AgentName}` | Display name of the agent the maker tried to create |
| `{EnvironmentName}` | Display name of the environment |
| `{HarnessName}` | Display name of the harness the maker tried, e.g. "GitHub Copilot" |
| `{AllowedHarnesses}` | Display names of the allowed harnesses, joined in natural language, e.g. "Standard or Copilot Chat" |

Below is the value shipped in the solution. Replace the image, texts and links with your own governance guidance.

```json
{
  "FullBlock": {
    "CardImageLink": "https://sappg.blob.core.windows.net/images/bac%20-%20adaptive%20card%20banner.png?sp=r&st=2026-07-15T11:15:18Z&se=2040-12-31T20:30:18Z&spr=https&sv=2026-02-06&sr=b&sig=7%2FfjV87OP6kVTlwhmCqpRKD4xXXT405wcibhSeb%2BRDI%3D",
    "TitleText": "🛑 Agent creation blocked in this environment",
    "IntroText": "Hi, we noticed you tried to create the agent **{AgentName}** in **{EnvironmentName}**. This environment isn't approved for building Copilot Studio agents, so your request was blocked. No action is needed on your side.",
    "NextStepsText": "To build your agent, please switch to an **approved environment**. If you're not sure which one to use, or you believe this environment *should* be approved, the resources below will point you in the right direction. Our CoE team is happy to help.",
    "Links": [
      { "Title": "📄 View approved environments & governance", "Url": "https://powertricks.io", "Style": "positive" },
      { "Title": "✉️ Contact the CoE team", "Url": "mailto:Power.Platform@powertricks.io" }
    ]
  },
  "PartialBlock": {
    "TitleText": "🛑 {HarnessName} agents cannot be created in this environment",
    "IntroText": "Hi, we noticed you tried to create the agent **{AgentName}** as a **{HarnessName}** agent in **{EnvironmentName}**. This environment isn't approved for building this type of agent, so your request was blocked. No action is needed on your side.",
    "NextStepsText": "To build your agent, please switch to an **approved environment** or create it as a **{AllowedHarnesses}** agent instead. If you're not sure which one to use, or you believe this environment *should* be approved, the resources below will point you in the right direction. Our CoE team is happy to help."
  }
}
```

The flow also still accepts the single-card shape of the previous version (the fields at the root, without `FullBlock` and `PartialBlock`), but partial blocks then get the full block wording.

## Keeping the control enabled
Any user with the Environment Maker role can disable a plugin step, which would silently switch off the control. The satellite's `bac - Auto Turn On Plugin Step` flow watches the step and re-enables it automatically; makers do not have access to that flow. Disabling the control on purpose is therefore a deliberate admin action:
- Turn off `bac - Auto Turn On Plugin Step` first, then disable the plugin step, or
- Delete the satellite solution from the environment altogether.

## The attempt table
Every attempt (blocked or allowed) is written to `ptricks_agentcreationattempt` in the central environment and can be reviewed through the `Agent Creation Attempt` model-driven app. Key columns:

| Column | Content |
|---|---|
| `ptricks_agentdisplayname` | Name of the agent the maker tried to create |
| `ptricks_attemptedby` / `ptricks_attemptedbyentraobjectid` | The maker (lookup and Entra object id) |
| `ptricks_environmentdisplayname` / `ptricks_environmentid` | Environment where creation was attempted |
| `ptricks_attemptedon` | Timestamp of the attempt |
| `ptricks_blocked` | Whether the attempt was blocked or allowed |
| `ptricks_agenttemplate` | Raw `bot.template` value received by the plugin |
| `ptricks_harness` / `ptricks_harnessdisplayname` | Resolved harness, as key (e.g. `GitHubCopilot`) and display name (e.g. "GitHub Copilot") |
| `ptricks_blockscope` | `FullBlock`, `PartialBlock` or `None`; selects which notification card is sent |
| `ptricks_allowedharnesses` | Allowed harnesses at the time of the attempt, as worded for the maker |
| `ptricks_notificationstatus` | Teams notification state (e.g. Not needed / Completed) |

![Agent Creation Attempt view listing a full block, a partial block, and an allowed attempt with their harness and block scope](Screenshots/bac-AttemptLogTable.png)

## Upgrading from 2026.09.04.1
Version `2026.11.23.0` replaces the Yes/No GitHub Copilot harness-only setting with the allowed harnesses list. It removes:

- `ptricks_AgentCreationBlockGitHubCopilotHarnessOnly` (Satellite), replaced by `ptricks_AgentCreationAllowedHarnesses`;
- `ptricks_AgentCreationCardSettingsGHCOnly` (Core), replaced by the `PartialBlock` key of `ptricks_AgentCreationCardSettings`;
- the `ptricks_isblockinggithubcopilotharnessonly` column (Core) and its data, replaced by `ptricks_blockscope` and four other new columns.

Follow these steps in order:

1. **Note the current settings before importing**, since the upgrade deletes them:
   - in each satellite environment, the value of `ptricks_AgentCreationBlockGitHubCopilotHarnessOnly`;
   - in the central environment, the value of `ptricks_AgentCreationCardSettingsGHCOnly`.
2. **Check for unmanaged customizations** on the Agent Creation Attempt form and views. One that still references the old column blocks the deletion during the upgrade.
3. **Import Core first**, as an **upgrade** (`pac solution import --stage-and-upgrade`, or "Upgrade" in the portal). An "Update" leaves the retired column and variables behind. Core must come first: the previous logging flow requires a field the new plugin no longer sends.
4. **Set `ptricks_AgentCreationCardSettings` to the two-key value** (see [Notification cards](#notification-cards)). Move the old GHC-only card text into `PartialBlock`, replacing "GitHub Copilot" with `{HarnessName}` where it appears. Until this is done, partial blocks get the full block wording.
5. **Import the Satellite straight after**, also as an **upgrade**, in each blocked environment. Set `ptricks_AgentCreationAllowedHarnesses` during the import (for example with a deployment settings file), translating the old setting:

   | Old `…BlockGitHubCopilotHarnessOnly` | New `ptricks_AgentCreationAllowedHarnesses` |
   |---|---|
   | `Yes` | `Standard,CopilotChat,CustomerConversation` (same behavior as before), or `Standard,CopilotChat` to also block Customer Conversation |
   | `No` / blank | *(blank)* |

   If the plugin lands before the variable has its value, the list is blank and every harness is fully blocked.
6. **Optionally switch `ptricks_AgentCreationBlockMessage` to the two-key JSON** (see [Block message](#block-message)). The plain-text value keeps working for both block scopes.

Between steps 3 and 5, blocks from the previous plugin still work, and partial blocks get the full block card. Attempts logged before the upgrade have no block scope and also get the full block card if they are ever re-notified.

See the [blog post](https://www.powertricks.io/block-agent-creation) for how to use this signal for governance and enablement.
