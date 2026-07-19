namespace RevitAssistant;

/// <summary>
/// Spawns and supervises the vendored TypeScript MCP server child process (spec §5.1, §7 Phase 2):
/// start on pane open, health-check, restart on crash, and kill on Revit exit.
/// </summary>
public sealed class ServerProcessManager : IDisposable
{
    /// <summary>Start the MCP server process. TODO(spec §7 Phase 2).</summary>
    public Task StartAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>Stop and dispose the server process. TODO(spec §7 Phase 2).</summary>
    public void Dispose() => throw new NotImplementedException();
}
