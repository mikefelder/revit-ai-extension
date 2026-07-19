using AgentCore;

// Console harness over AgentCore (spec §7, Phase 1).
// TODO: load config, start McpToolBroker, build AgentLoop, and run a REPL that streams
// responses, auto-executes reads, and prompts y/n for writes via ConsoleApprovalHandler.
Console.WriteLine("Revit AI Agent — console harness (scaffold; not yet implemented).");
Console.WriteLine("See docs/revit-ai-agent-spec.md §7 Phase 1 for the acceptance criteria.");

namespace ConsoleHarness
{
    /// <summary>Console y/n implementation of the write-approval gate (spec §5.2).</summary>
    internal sealed class ConsoleApprovalHandler : IApprovalHandler
    {
        public Task<ApprovalVerdict> RequestAsync(ApprovalRequest request, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    /// <summary>Stub context provider returning "console mode" context (spec §7 Phase 1).</summary>
    internal sealed class ConsoleContextProvider : IRevitContextProvider
    {
        public Task<RevitContextSnapshot> SnapshotAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
