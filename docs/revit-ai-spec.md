# Revit AI Agent — v1 Architecture & Development Spec

> **Audience:** This document is a build spec intended to be executed by an AI development agent
> (e.g., Claude Code) working in a fresh repository on a Windows machine. Follow phases in order.
> Do not skip acceptance criteria. When uncertain about a Revit API member, verify against
> official documentation rather than guessing — the Revit API is large and hallucinated members
> are a known failure mode.

---

## 1. Project summary

**Goal:** A "GitHub Copilot extension for Xcode"-style AI assistant for Autodesk Revit: a native
dockable chat panel inside Revit, backed by a model deployed in **Azure AI Foundry**, that can
read, analyze, and (with explicit user approval) modify the open Revit model via the
**Model Context Protocol (MCP)**.

**Non-goals for v1:**
- Not a commercial product. Single-user, personal/hobby deployment. No licensing, billing,
  multi-tenancy, or telemetry beyond local logs.
- No free-form AI-generated code execution inside Revit (`execute_code`-style tools are
  explicitly out of scope for v1 — they are the least reliable and most dangerous surface).
- No cloud-side storage of model data. All Revit data stays on the local machine except what is
  sent to the Foundry model endpoint as conversation context.

**Primary user story:** An architecture/BIM professional opens a project in Revit, opens the
"Assistant" panel, and asks things like:
- "How many doors on Level 2 are missing a fire rating parameter?"
- "List all walls with warnings and summarize the issue types."
- "Generate a QC summary of this model."
- "Rename views matching 'Copy of *' to remove the prefix." *(write — requires approval)*

---

## 2. Architecture overview

```
┌────────────────────────────── Windows PC ──────────────────────────────┐
│                                                                        │
│  ┌───────────────────── Autodesk Revit (2026) ─────────────────────┐   │
│  │                                                                 │   │
│  │  ┌──────────────────────┐        ┌─────────────────────────┐    │   │
│  │  │  Assistant Add-in    │        │  MCP Bridge Add-in      │    │   │
│  │  │  (NEW — this repo)   │        │  (fork of               │    │   │
│  │  │  WPF dockable panel  │        │  mcp-servers-for-revit) │    │   │
│  │  │  + agent loop        │        │  WebSocket bridge +     │    │   │
│  │  │  + MCP client        │        │  command set, executes  │    │   │
│  │  └─────────┬────────────┘        │  Revit API ops via      │    │   │
│  │            │                     │  ExternalEvent          │    │   │
│  │            │                     └───────────▲─────────────┘    │   │
│  └────────────┼─────────────────────────────────┼──────────────────┘   │
│               │                                 │ WebSocket            │
│               │ MCP (stdio)      ┌──────────────┴───────────┐          │
│               └─────────────────►│  revit-mcp server (TS,   │          │
│                                  │  from the fork, runs as  │          │
│                                  │  a child process)        │          │
│                                  └──────────────────────────┘          │
│               │                                                        │
└───────────────┼────────────────────────────────────────────────────────┘
                │ HTTPS (OpenAI-compatible chat completions, streaming,
                │        tool calling)
                ▼
     ┌─────────────────────────┐
     │  Azure AI Foundry       │
     │  project + model        │
     │  deployment             │
     │  (auth: API key or      │
     │   Entra ID)             │
     └─────────────────────────┘
```

### 2.1 Key architectural decisions (do not re-litigate without flagging)

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | **Client-side agent loop.** The orchestration loop (model call → tool call → tool result → model call) runs inside the Assistant add-in process. Foundry provides only the model endpoint. | Foundry Agent Service's server-side MCP tool support requires the MCP server to be reachable at a public URL. The Revit MCP server is localhost-only. A client-side loop avoids exposing model data via dev tunnels. Devtunnel + hosted Agent Service is a documented later option (§9). |
| D2 | **Fork `mcp-servers-for-revit` for the execution layer.** Do not write a new Revit↔AI bridge. | The WebSocket plugin, command set, ExternalEvent marshaling (Revit API is single-threaded), and multi-version build targets are already solved and maintained there. Net-new code = panel + agent loop only. |
| D3 | **Reads auto-execute; writes require explicit approval** in the panel, per call. | Reliability of AI-driven writes is the known weak point across all tools in this space. Approval gating + transactions makes failures recoverable. |
| D4 | **Whitelisted write tools only** in v1 (see §5.3). No arbitrary code execution. | Same reliability reasoning. Expand the whitelist deliberately, one tool at a time. |
| D5 | **C# / .NET 8** for all new code; model access via the OpenAI-compatible endpoint (`OpenAI` / `Azure.AI.OpenAI` NuGet); MCP client via the official **ModelContextProtocol C# SDK**. | One language across the add-in; both SDKs are first-party and current. Keeps the Foundry model swappable (GPT-4.1, GPT-5-class, or Claude via Foundry model catalog) without code changes. |
| D6 | **Target Revit 2026 (confirmed — the target firm runs 2026).** Version remains configurable, but 2026 is the primary build/test target. | The fork supports 2020–2026 (net48 for ≤2024, net8 for ≥2025); 2026 means all new code and the fork build target are .NET 8 throughout. |

### 2.2 Components

1. **`revit-mcp` fork (vendored)** — the upstream monorepo (`mcp-servers-for-revit/mcp-servers-for-revit`):
   Revit add-in (C#) exposing a WebSocket bridge + command set executing Revit API operations,
   plus a TypeScript MCP server exposing those commands as MCP tools over stdio.
   Used essentially as-is in v1. Verify its license (expected MIT) and retain attribution.
2. **Assistant add-in (new)** — C#/.NET 8 Revit add-in registering a **WPF dockable pane**:
   chat UI, streaming markdown rendering, tool-call plan cards with Approve/Reject, settings.
3. **Agent core (new)** — a plain C# class library (no Revit references) implementing the
   agent loop: conversation state, Foundry chat-completions client, MCP client session,
   tool-call routing, approval hooks, cancellation, logging. Kept Revit-free so it is unit-testable
   and reusable from a console harness.
4. **Console harness (new, Phase 1)** — thin CLI over the agent core for development before the
   UI exists, and for regression testing after.
5. **Azure AI Foundry project** — one model deployment. Endpoint + deployment name + auth
   supplied via config (§6). Created manually by the human (not by the dev agent).

---

## 3. Repository layout

```
revit-ai-agent/
├── README.md
├── SPEC.md                        # this document
├── .gitignore                     # VS/.NET + node + secrets
├── config/
│   └── appsettings.sample.json   # documented sample; real config is git-ignored
├── vendor/
│   └── mcp-servers-for-revit/    # git submodule or subtree of the fork
├── src/
│   ├── AgentCore/                # class library, net8.0, NO Revit refs
│   │   ├── AgentLoop.cs
│   │   ├── FoundryChatClient.cs
│   │   ├── McpToolBroker.cs      # MCP session, tool discovery, read/write classification
│   │   ├── Approval.cs           # IApprovalHandler abstraction
│   │   ├── ContextProvider.cs    # IRevitContextProvider abstraction
│   │   ├── Transcript.cs         # conversation state + persistence
│   │   └── Config.cs
│   ├── ConsoleHarness/           # net8.0 console app referencing AgentCore
│   └── RevitAssistant/           # Revit add-in, net8.0-windows, WPF
│       ├── App.cs                # IExternalApplication, registers dockable pane
│       ├── AssistantPane.xaml(.cs)
│       ├── ViewModels/
│       ├── Controls/             # ChatMessageControl, ToolPlanCard, etc.
│       ├── RevitContextProvider.cs  # implements IRevitContextProvider via Revit API
│       ├── ServerProcessManager.cs  # spawns/monitors the TS MCP server child process
│       └── RevitAssistant.addin
├── tests/
│   └── AgentCore.Tests/          # xUnit; mock model + mock MCP server
└── tools/
    └── install.ps1               # copies add-in outputs + .addin manifests to %AppData%
```

---

## 4. Environment & prerequisites

All development and runtime on **Windows 11** (Revit is Windows-only).
Target machine class: Ryzen 5800X3D / 64 GB RAM — more than sufficient; the GPU is irrelevant
to this project.

Required (install/verify in Phase 0):
- Autodesk Revit 2026 (or confirmed target version) with a sample project
  (e.g., the bundled `Snowdon Towers` / `rac_advanced_sample_project.rvt`).
- .NET 8 SDK; Visual Studio 2022 (or Build Tools) with desktop workload.
- Node.js ≥ 18 (for the TS MCP server in the fork).
- Git; the fork added as submodule under `vendor/`.
- An Azure AI Foundry project with one chat model deployed. Human supplies:
  endpoint URL, deployment name, API key (or uses `DefaultAzureCredential`).
- (Phase 0 only) Claude Desktop or another MCP client, used purely to smoke-test the fork
  before any new code is written.

---

## 5. Functional specification

### 5.1 Chat panel (RevitAssistant)

- Dockable pane (Revit `IDockablePaneProvider`), default docked right, ~380 px min width.
- Message list: user / assistant / tool-event entries. Assistant messages render markdown
  (headings, lists, tables, code blocks). Streaming: tokens appear as they arrive.
- Input box with Enter-to-send, Shift+Enter newline, and a Stop button that cancels the
  in-flight loop (CancellationToken through the whole stack).
- **Context strip** at top of panel showing what will be injected this turn: document title,
  active view name, selection count. Refreshed each send (§5.4).
- **Tool activity:** each tool call renders as a card: tool name, human-readable summary of
  arguments, status (pending / running / done / failed), collapsible raw JSON.
- **Approval flow (writes):** write-classified tool calls pause the loop and render the card
  with Approve / Reject buttons and a plain-English description of what will change
  (e.g., "Rename 14 views: 'Copy of L1' → 'L1', …"). Reject returns a structured refusal to
  the model as the tool result so it can continue gracefully.
- Session transcript persisted to `%LocalAppData%/RevitAssistant/sessions/*.json`;
  "New chat" button; last session reload optional (stretch).
- Settings flyout: endpoint/deployment (read-only display), system prompt override,
  temperature, "auto-approve reads" (always on in v1), log level.

### 5.2 Agent loop (AgentCore)

Pseudocode contract:

```
send(userText):
  context = contextProvider.Snapshot()            # doc, view, selection, units
  messages += system(basePrompt + context) if first turn else contextRefresher(context)
  messages += user(userText)
  loop (max 12 iterations):
    resp = foundry.ChatStream(messages, tools=mcpToolSchemas)
    stream text deltas to UI
    if resp has tool_calls:
      for each call:
        if broker.IsWrite(call.name):
          verdict = approvalHandler.Request(call)  # UI card; console: y/n prompt
          if rejected: result = {"rejected_by_user": true, reason}
          else: result = mcp.CallTool(call)
        else:
          result = mcp.CallTool(call)
        messages += toolResult(result, truncated to N chars with note if larger)
      continue loop
    else: break
```

Requirements:
- Tool schemas come from MCP `tools/list` at startup and are converted to OpenAI
  function-calling JSON schema. No hand-maintained duplicate schemas.
- Tool results larger than a configurable cap (default 20 000 chars) are truncated with an
  explicit `[truncated — ask a narrower question or request pagination]` marker appended.
- Max-iteration and max-token guards; on hitting either, the assistant explains and stops.
- All model/tool traffic logged (Serilog) to rolling files at
  `%LocalAppData%/RevitAssistant/logs/`; log the tool args and result sizes, not full model
  payloads at Info level (full payloads at Debug).

### 5.3 Tool classification & v1 write whitelist

`McpToolBroker` classifies every discovered tool as `Read` or `Write`:
- Classification source: explicit allowlist/denylist in config, falling back to name
  heuristics (`get_*`, `list_*`, `query_*`, `export_*` → Read; everything else → Write).
  **Unknown tools default to Write** (i.e., gated).
- v1 **enabled** write tools (edit config, not code, to change): view renaming, parameter
  set on selected elements, view/sheet creation, tagging. Everything else Write-classified
  is **disabled** (not offered to the model at all) in v1 — including element deletion and
  any generic code-execution tool the fork may expose. Disabled = excluded from the tool
  schema list sent to the model.

### 5.4 Revit context injection

`RevitContextProvider` snapshot (cheap calls only, on the UI thread via ExternalEvent if
required by API context):
- Document title, file path, Revit version, project units.
- Active view: name, type, associated level if any.
- Current selection: count + up to 20 entries of `(ElementId, Category, Name)`.
- Injected as a fenced block in the system/context message each turn. This is the
  "Copilot reads your active file" equivalent and is a hard requirement, not a stretch goal.

### 5.5 System prompt (base, editable in settings)

Must include, at minimum:
- Role: assistant embedded in Autodesk Revit, operating on the currently open model via tools.
- Honesty about limits: prefer tool calls over recall; never invent element data; if a needed
  tool doesn't exist, say so.
- Write discipline: before any write tool call, state in one sentence what will change and why;
  expect that the user may reject it; never chain multiple write calls without intermediate
  results.
- Output style: concise; tables for element listings; totals stated explicitly.

---

## 6. Configuration

`config/appsettings.json` (git-ignored; sample committed):

```json
{
  "foundry": {
    "endpoint": "https://<project>.openai.azure.com/",
    "deployment": "<deployment-name>",
    "auth": "apiKey | entra",
    "apiKeyEnvVar": "REVIT_ASSISTANT_FOUNDRY_KEY",
    "maxOutputTokens": 4096,
    "temperature": 0.2
  },
  "mcp": {
    "serverCommand": "node",
    "serverArgs": ["vendor/mcp-servers-for-revit/server/build/index.js"],
    "startupTimeoutSeconds": 20
  },
  "tools": {
    "readAllowlist": [],
    "writeEnabled": ["rename_views", "set_parameter", "create_sheet", "tag_elements"],
    "resultCharCap": 20000
  },
  "revit": { "targetVersion": "2026" },
  "logging": { "level": "Information" }
}
```

Secrets only via environment variables or Entra ID — never in the repo, never in logs.
(Tool names in `writeEnabled` above are placeholders — replace with the fork's actual tool
names discovered in Phase 0.)

---

## 7. Development plan (phased, with acceptance criteria)

### Phase 0 — Environment validation & upstream smoke test *(no new code)*

Tasks:
1. Install prerequisites (§4). Confirm Revit launches and opens the sample project.
2. Add the fork as `vendor/` submodule. Build it per its README for the target Revit version.
   Install its add-in; confirm the MCP switch/indicator works inside Revit.
3. Connect **Claude Desktop** (or any MCP client) to the fork's server. Run and record results
   for: list walls, count doors per level, get warnings, export a schedule.
4. Produce `docs/phase0-report.md`: exact tool names discovered via `tools/list`, which worked,
   response shapes/sizes, quirks (port conflicts, version pinning, .addin paths), and the
   verified upstream license.

**Acceptance:** A documented, reproducible read-query session against the sample model through
the unmodified fork. The real tool-name list feeds §5.3/§6.

### Phase 1 — AgentCore + console harness

Tasks:
1. Scaffold solution/projects per §3. Wire NuGet: `OpenAI`/`Azure.AI.OpenAI`,
   `ModelContextProtocol`, `Serilog`, `xUnit`.
2. Implement `FoundryChatClient` (streaming chat completions with tool calling against the
   Foundry deployment).
3. Implement `McpToolBroker`: spawn server process, MCP handshake, `tools/list`,
   schema conversion, read/write classification per config, `CallTool`.
4. Implement `AgentLoop` per §5.2 with `IApprovalHandler` (console: y/n) and a stub
   `IRevitContextProvider` (returns "console mode" context).
5. Unit tests: schema conversion; classification defaults (unknown → Write); loop behavior on
   tool rejection; truncation; max-iteration guard. Mock model + mock MCP server (in-process).
6. Manual end-to-end: with Revit + fork running, run the console harness and complete the four
   Phase-0 queries **plus one gated write** (approve path and reject path).

**Acceptance:** `dotnet test` green; console session transcript demonstrating streamed answers,
an auto-executed read chain, and a write that was (a) blocked pending approval, (b) executed on
approve, (c) gracefully handled on reject.

### Phase 2 — Revit add-in with WPF dockable panel

Tasks:
1. `RevitAssistant` project: `IExternalApplication` + dockable pane registration + `.addin`
   manifest + `tools/install.ps1`.
2. Chat UI per §5.1 (MVVM; streaming rendering; Stop/cancel; tool cards; approval cards).
3. `RevitContextProvider` per §5.4; `ServerProcessManager` (start server on pane open, health
   check, restart on crash, kill on Revit exit).
4. Threading: all Revit API access via ExternalEvent; all model/network IO off the UI thread;
   UI updates marshaled back. Document the threading model in `docs/threading.md`.
5. Failure UX: Foundry unreachable, MCP server dead, no document open — each produces a clear
   in-panel message, never a crash of Revit.

**Acceptance:** From a clean machine following README only: build, run `install.ps1`, open
Revit, open panel, and reproduce the Phase-1 end-to-end (reads + gated write) entirely in-panel,
including context strip correctness after switching views and changing selection. Revit remains
stable through server kill/restart and mid-stream Stop.

### Phase 3 — Reliability & the flagship QC workflow

Tasks:
1. Add a **"Model QC report"** slash-command / button: a curated multi-tool prompt producing a
   structured report (warnings by type, elements missing key parameters, unplaced rooms,
   duplicate view names, etc. — final checklist derived from what the fork's read tools support,
   per Phase 0 report) rendered as markdown in-panel with a "Save as .md" export.
2. Add **Revit API docs grounding**: integrate a docs-search MCP server (e.g., `Rvt_Docs_MCP`)
   as a second MCP session, exposed to the model as read tools — used when the user asks "how do
   I…" product/API questions. If integration cost is high, fallback: none in v1; the assistant
   answers capability questions only from its tool list.
3. Prompt-regression suite: 10 canonical prompts with expected-behavior assertions
   (tool-call sequence shape, not exact text) runnable via the console harness against the
   sample model; documented in `tests/prompts.md`.

**Acceptance:** QC report runs end-to-end on the sample model in one click; regression suite
documented and passing; (if included) a docs question answered with a citation to the docs tool
result.

### Phase 4 — Polish & handover

Tasks:
1. Settings flyout complete; transcript persistence; "New chat".
2. README: architecture diagram, install guide (including gf's-machine install path),
   config reference, troubleshooting (from Phase 0/2 findings), upstream attribution/licenses.
3. `docs/backlog.md`: deferred items — devtunnel + Foundry Agent Service hosting (§9),
   expanded write whitelist, selection-driven "fix this" flows, firm-standards checks,
   auto-approve rules, additional Revit versions.

**Acceptance:** A non-author can install and use the assistant on a second Windows machine using
only the README.

---

## 8. Guardrails for the AI development agent

- **Never invent Revit API members.** If Phase 0's docs are insufficient, consult
  revitapidocs.com / official docs, or route work through the fork's existing command set
  instead of writing new Revit API code.
- New Revit API code (Phase 2 context provider only, in v1) must run inside valid API context
  (ExternalEvent) and never block the Revit UI thread on network IO.
- Do not modify vendored fork code in v1 except build-config fixes; if a change is unavoidable,
  isolate it in a clearly marked commit and note it in the README.
- Every write path must be wrapped in a Revit `Transaction` (the fork's command set does this —
  verify in Phase 0 and note any tool that doesn't; such tools stay disabled).
- Keep `AgentCore` free of Revit and WPF references — enforced by the test project compiling it
  on plain `net8.0`.
- Commit at the end of each numbered task with a message referencing the spec section.
- If an assumption in this spec conflicts with observed reality (tool names, fork architecture,
  SDK API shape), record the discrepancy in `docs/deviations.md` and proceed with the observed
  reality.

## 9. Deferred: hosted-agent variant (documented, not built)

When/if multi-machine or remote use matters: expose the local MCP server via Microsoft Dev
Tunnels (authenticated), register it as an MCP tool on a Foundry Agent Service agent, and thin
the local panel down to a UI over the hosted agent's thread API. Trade-offs: model data transits
the tunnel; approval flow must move to Foundry's tool-approval mechanism. Out of scope for v1.

## 10. Open questions for the human (answer before the phase noted)

1. ~~Target Revit version~~ — **answered: Revit 2026.**
2. **Foundry model choice** for the deployment (any OpenAI-compatible chat model with tool
   calling works; pick one with strong function-calling reliability).
3. Whether the gf's work machine can have unsigned local add-ins installed (affects Phase 4
   install docs; some firms lock this down — in that case the project stays a home-machine demo).
