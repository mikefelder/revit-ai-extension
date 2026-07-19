# Revit AI Agent

A "GitHub Copilot for Xcode"-style AI assistant for Autodesk Revit: a native dockable chat
panel inside Revit, backed by a model deployed in **Azure AI Foundry**, that can read, analyze,
and (with explicit user approval) modify the open Revit model via the **Model Context Protocol
(MCP)**.

> **Source of truth:** [`docs/revit-ai-agent-spec.md`](docs/revit-ai-agent-spec.md).
> This README is a short orientation; the spec governs all architecture and scope decisions.

## Status

Repository scaffold only — project structure and skeleton types are in place; **no logic is
implemented yet**. Build wiring (NuGet versions, the vendored fork) is resolved in Phase 0/1 of
the spec.

## Layout

```
config/     Sample configuration (real appsettings.json is git-ignored)
docs/       Spec and design docs
src/
  AgentCore/        net8.0 class library — agent loop, Foundry client, MCP broker (NO Revit refs)
  ConsoleHarness/   net8.0 console app over AgentCore (dev + regression harness)
  RevitAssistant/   net8.0-windows WPF Revit add-in (dockable panel)
tests/
  AgentCore.Tests/  xUnit tests for AgentCore
tools/      install.ps1 and dev tooling
vendor/     mcp-servers-for-revit fork (added as a submodule in Phase 0)
```

## Prerequisites (Windows 11)

- Autodesk Revit 2026 with a sample project
- .NET 8 SDK + Visual Studio 2022 (desktop workload)
- Node.js >= 18 (for the vendored TS MCP server)
- An Azure AI Foundry project with one chat model deployed

## Build

> Note: `RevitAssistant` targets `net8.0-windows` (WPF + Revit API) and builds on **Windows
> only**. `AgentCore`, `ConsoleHarness`, and the tests are plain `net8.0` and are
> cross-platform.

```pwsh
dotnet build RevitAiAgent.sln
```

## Package versions

The `PackageReference` versions in the `.csproj` files are provisional placeholders and **must
be verified/pinned** against the actual current releases during Phase 0/1 (see the spec).

## Attribution

The execution layer is a fork of `mcp-servers-for-revit` (vendored under `vendor/`). Verify its
license (expected MIT) and retain attribution before distribution.
