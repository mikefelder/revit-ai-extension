using AgentCore;
using ConsoleHarness;

// Console harness over AgentCore (spec §7, Phase 1): loads config, starts the MCP broker, builds
// the agent loop, and runs a REPL that streams responses, auto-executes reads, and prompts y/n
// for writes via ConsoleApprovalHandler.
//
// Flags:
//   --tools   Phase 0 diagnostic: start the MCP broker, print discovered/enabled tools with their
//             Read/Write classification, call one read tool, then exit. Does not use Foundry.

var toolsOnly = args.Contains("--tools");
var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var configPath = positional.Length > 0 ? positional[0] : Path.Combine("config", "appsettings.json");

AppConfig config;
try
{
    config = AppConfig.Load(configPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load config '{configPath}': {ex.Message}");
    Console.Error.WriteLine("Pass a config path as the first argument, or create config/appsettings.json "
        + "from config/appsettings.sample.json.");
    return 1;
}

await using var broker = new McpToolBroker(config.Mcp, config.Tools);

Console.WriteLine("Starting MCP server and completing handshake…");
try
{
    await broker.StartAsync(CancellationToken.None);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not start the MCP server: {ex.Message}");
    Console.Error.WriteLine("Ensure Revit is running with the MCP bridge add-in, and that "
        + "mcp.serverCommand/serverArgs in the config point at the built server.");
    return 1;
}

if (toolsOnly)
{
    var tools = await broker.ListEnabledToolsAsync(CancellationToken.None);
    Console.WriteLine($"\nDiscovered {tools.Count} enabled tool(s):");
    foreach (var tool in tools.OrderBy(t => t.Kind).ThenBy(t => t.Name))
        Console.WriteLine($"  [{tool.Kind,-5}] {tool.Name} — {tool.Description}");

    var probe = tools.FirstOrDefault(t => t.Name == "get_current_view_info")
        ?? tools.FirstOrDefault(t => t.Kind == ToolKind.Read);
    if (probe is null)
    {
        Console.WriteLine("\nNo read tool available to probe.");
        return 0;
    }

    Console.WriteLine($"\nCalling read tool '{probe.Name}'…");
    try
    {
        var result = await broker.CallToolAsync(
            new ToolCall("probe-1", probe.Name, "{}"), CancellationToken.None);
        var preview = result.Content.Length > 2000 ? result.Content[..2000] + "…" : result.Content;
        Console.WriteLine(preview.Length == 0 ? "(empty result)" : preview);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Read call failed: {ex.Message}");
        return 1;
    }

    return 0;
}

var foundry = new FoundryChatClient(config.Foundry);
var transcript = new Transcript();
var loop = new AgentLoop(
    foundry, broker, new ConsoleApprovalHandler(), new ConsoleContextProvider(), transcript)
{
    MaxIterations = 12,
};

Console.WriteLine("Revit AI Agent — console harness. Type a message, '/exit' to quit.");

var sink = new ConsoleAgentEventSink();
while (true)
{
    Console.Write("\n> ");
    var line = Console.ReadLine();
    if (line is null || line.Trim() is "/exit" or "/quit")
        break;
    if (string.IsNullOrWhiteSpace(line))
        continue;

    using var cts = new CancellationTokenSource();
    void OnCancel(object? _, ConsoleCancelEventArgs e) { e.Cancel = true; cts.Cancel(); }
    Console.CancelKeyPress += OnCancel;
    try
    {
        await loop.SendAsync(line, sink, cts.Token);
    }
    finally
    {
        Console.CancelKeyPress -= OnCancel;
    }
    Console.WriteLine();
}

return 0;

namespace ConsoleHarness
{
    /// <summary>Console y/n implementation of the write-approval gate (spec §5.2).</summary>
    internal sealed class ConsoleApprovalHandler : IApprovalHandler
    {
        public Task<ApprovalVerdict> RequestAsync(ApprovalRequest request, CancellationToken ct)
        {
            Console.WriteLine();
            Console.WriteLine($"[approval] Write tool requested: {request.ToolName}");
            Console.WriteLine($"           {request.HumanSummary}");
            Console.Write("           Approve? [y/N]: ");
            var answer = Console.ReadLine()?.Trim();
            var approved = answer is "y" or "Y" or "yes";
            return Task.FromResult(new ApprovalVerdict(
                approved, approved ? null : "User declined at the console."));
        }
    }

    /// <summary>Stub context provider returning "console mode" context (spec §7 Phase 1).</summary>
    internal sealed class ConsoleContextProvider : IRevitContextProvider
    {
        public Task<RevitContextSnapshot> SnapshotAsync(CancellationToken ct) =>
            Task.FromResult(new RevitContextSnapshot(
                DocumentTitle: "(console mode — no live Revit document)",
                DocumentPath: null,
                RevitVersion: "console",
                ProjectUnits: "unknown",
                ActiveViewName: null,
                ActiveViewType: null,
                ActiveViewLevel: null,
                SelectionCount: 0,
                Selection: []));
    }

    /// <summary>Writes streamed tokens and tool activity to the console (spec §5.1).</summary>
    internal sealed class ConsoleAgentEventSink : IAgentEventSink
    {
        public void OnTextDelta(string text) => Console.Write(text);

        public void OnToolStarted(ToolCall call, ToolKind kind) =>
            Console.WriteLine($"\n[tool:{kind.ToString().ToLowerInvariant()}] {call.Name} {call.ArgumentsJson}");

        public void OnToolFinished(ToolCall call, ToolResult result)
        {
            var status = result.RejectedByUser ? "rejected" : result.Truncated ? "done (truncated)" : "done";
            Console.WriteLine($"[tool:{status}] {call.Name}");
        }

        public void OnCompleted() { }

        public void OnError(Exception ex) => Console.Error.WriteLine($"\n[error] {ex.Message}");
    }
}
