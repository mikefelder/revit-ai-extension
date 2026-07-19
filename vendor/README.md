# vendor/

The execution layer is provided by a fork of **`mcp-servers-for-revit`**
(`mcp-servers-for-revit/mcp-servers-for-revit`): a Revit add-in (C#) exposing a WebSocket bridge
+ command set that executes Revit API operations via `ExternalEvent`, plus a TypeScript MCP
server exposing those commands as MCP tools over stdio.

## To be added in Phase 0

Add the fork as a git submodule here, e.g.:

```pwsh
git submodule add <your-fork-url> vendor/mcp-servers-for-revit
```

Then build it per its README for Revit 2026 (net8) and record the discovered tool names in
`docs/phase0-report.md` (see spec §7, Phase 0).

Do not modify vendored code in v1 except build-config fixes (spec §8).
