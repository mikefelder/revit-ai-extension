<#
.SYNOPSIS
  Installs the Revit AI Assistant add-in (spec §7 Phase 2 / Phase 4).
.DESCRIPTION
  Copies the RevitAssistant build output and the .addin manifest into the per-user Revit Addins
  folder for the target Revit version. Windows + PowerShell only.
  Scaffold: fill in during Phase 2.
#>
param(
    [string]$Configuration = "Release",
    [string]$RevitVersion  = "2026"
)

$ErrorActionPreference = "Stop"

$addinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
Write-Host "Target Revit Addins folder: $addinsDir"

# TODO(spec §7 Phase 2):
#   1. Build src/RevitAssistant in $Configuration.
#   2. Copy RevitAssistant.dll (+ dependencies) into $addinsDir\RevitAssistant\.
#   3. Copy RevitAssistant.addin into $addinsDir, pointing <Assembly> at the copied dll.
#   4. Verify the WebView2 runtime / prerequisites as needed.

Write-Warning "install.ps1 is a scaffold — implement in Phase 2 (see docs/revit-ai-agent-spec.md §7)."
