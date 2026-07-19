using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AgentCore;

/// <summary>
/// Read/write tool classification and invocation over an MCP session, decoupled from the concrete
/// transport so the agent loop is unit-testable with a mock broker (spec §7).
/// </summary>
public interface IToolBroker
{
    /// <summary>Discovered tools converted to OpenAI function schemas, excluding disabled writes.</summary>
    Task<IReadOnlyList<ToolSchema>> ListEnabledToolsAsync(CancellationToken ct);

    /// <summary>Classify a tool as Read or Write (unknown => Write).</summary>
    ToolKind Classify(string toolName);

    bool IsWrite(string toolName);

    /// <summary>Invoke a tool and return its (possibly truncated) result.</summary>
    Task<ToolResult> CallToolAsync(ToolCall call, CancellationToken ct);
}

/// <summary>
/// Owns the MCP client session (spec §2.2, §5.3): spawns the vendored TypeScript MCP server as a
/// child process, performs the handshake, discovers tools via <c>tools/list</c>, converts their
/// schemas to OpenAI function-calling JSON schema, classifies each as Read or Write, and invokes
/// them. Tool schemas are never hand-maintained — they are derived from the live server.
/// </summary>
public sealed class McpToolBroker : IToolBroker, IAsyncDisposable
{
    private static readonly string[] ReadPrefixes = ["get_", "list_", "query_", "export_"];

    private readonly McpConfig _mcpConfig;
    private readonly ToolsConfig _toolsConfig;

    private IMcpClient? _client;
    private IList<McpClientTool>? _tools;

    public McpToolBroker(McpConfig mcpConfig, ToolsConfig toolsConfig)
    {
        _mcpConfig = mcpConfig;
        _toolsConfig = toolsConfig;
    }

    /// <summary>Start the server process and complete the MCP handshake (spec §5.3, Phase 1).</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "revit-mcp",
            Command = _mcpConfig.ServerCommand,
            Arguments = _mcpConfig.ServerArgs.ToArray(),
        });

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(_mcpConfig.StartupTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        _client = await McpClientFactory.CreateAsync(transport, cancellationToken: linked.Token);
        _tools = await _client.ListToolsAsync(cancellationToken: linked.Token);
    }

    /// <summary>
    /// Discovered tools converted to OpenAI function schemas, excluding disabled writes
    /// (spec §5.3: disabled = not offered to the model).
    /// </summary>
    public Task<IReadOnlyList<ToolSchema>> ListEnabledToolsAsync(CancellationToken ct)
    {
        if (_tools is null)
            throw new InvalidOperationException("Broker not started; call StartAsync first.");

        var schemas = new List<ToolSchema>();
        foreach (var tool in _tools)
        {
            var kind = Classify(tool.Name);
            if (kind == ToolKind.Write &&
                !_toolsConfig.WriteEnabled.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue; // write-classified but not whitelisted => not offered to the model
            }

            schemas.Add(new ToolSchema(
                tool.Name,
                tool.Description ?? string.Empty,
                tool.JsonSchema.GetRawText(),
                kind));
        }

        return Task.FromResult<IReadOnlyList<ToolSchema>>(schemas);
    }

    /// <summary>
    /// Classify a tool as Read or Write. Source: config allow/deny lists, falling back to name
    /// heuristics (get_/list_/query_/export_ => Read); unknown tools default to Write (gated)
    /// (spec §5.3).
    /// </summary>
    public ToolKind Classify(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return ToolKind.Write;

        if (_toolsConfig.ReadAllowlist.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return ToolKind.Read;

        if (_toolsConfig.WriteEnabled.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return ToolKind.Write;

        foreach (var prefix in ReadPrefixes)
            if (toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ToolKind.Read;

        return ToolKind.Write; // unknown => Write (gated)
    }

    public bool IsWrite(string toolName) => Classify(toolName) == ToolKind.Write;

    /// <summary>Invoke a tool and return its (possibly truncated) result (spec §5.2).</summary>
    public async Task<ToolResult> CallToolAsync(ToolCall call, CancellationToken ct)
    {
        if (_client is null)
            throw new InvalidOperationException("Broker not started; call StartAsync first.");

        var arguments = ParseArguments(call.ArgumentsJson);
        var result = await _client.CallToolAsync(call.Name, arguments, cancellationToken: ct);

        var text = ExtractText(result);
        return Truncate(text, _toolsConfig.ResultCharCap);
    }

    /// <summary>Truncate an oversized tool result with an explicit marker (spec §5.2).</summary>
    internal static ToolResult Truncate(string text, int cap)
    {
        if (text.Length <= cap)
            return new ToolResult(text, Truncated: false);

        var trimmed = text[..cap]
            + "\n[truncated — ask a narrower question or request pagination]";
        return new ToolResult(trimmed, Truncated: true);
    }

    private static IReadOnlyDictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object?>();

        using var doc = JsonDocument.Parse(argumentsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>();

        var dict = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();
        return dict;
    }

    private static string ExtractText(CallToolResult result)
    {
        var sb = new StringBuilder();
        foreach (var block in result.Content)
            if (block is TextContentBlock text)
                sb.AppendLine(text.Text);

        var content = sb.ToString().TrimEnd();
        if (result.IsError == true && content.Length == 0)
            content = "[tool reported an error with no message]";
        return content;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
    }
}

public enum ToolKind
{
    Read,
    Write,
}

/// <summary>An OpenAI-compatible function schema derived from an MCP tool.</summary>
public sealed record ToolSchema(string Name, string Description, string ParametersJsonSchema, ToolKind Kind);

/// <summary>The outcome of a tool invocation returned to the model as a tool message.</summary>
public sealed record ToolResult(string Content, bool Truncated, bool RejectedByUser = false);
