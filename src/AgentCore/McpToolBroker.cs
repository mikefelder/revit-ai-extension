namespace AgentCore;

/// <summary>
/// Owns the MCP client session (spec §2.2, §5.3): spawns the vendored TypeScript MCP server as a
/// child process, performs the handshake, discovers tools via <c>tools/list</c>, converts their
/// schemas to OpenAI function-calling JSON schema, classifies each as Read or Write, and invokes
/// them. Tool schemas are never hand-maintained — they are derived from the live server.
/// </summary>
public sealed class McpToolBroker : IAsyncDisposable
{
    private readonly McpConfig _mcpConfig;
    private readonly ToolsConfig _toolsConfig;

    public McpToolBroker(McpConfig mcpConfig, ToolsConfig toolsConfig)
    {
        _mcpConfig = mcpConfig;
        _toolsConfig = toolsConfig;
    }

    /// <summary>Start the server process and complete the MCP handshake. TODO(spec §5.3, Phase 1).</summary>
    public Task StartAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// Discovered tools converted to OpenAI function schemas, excluding disabled writes
    /// (spec §5.3: disabled = not offered to the model). TODO.
    /// </summary>
    public Task<IReadOnlyList<ToolSchema>> ListEnabledToolsAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    /// <summary>
    /// Classify a tool as Read or Write. Source: config allow/deny lists, falling back to name
    /// heuristics (get_/list_/query_/export_ => Read); unknown tools default to Write (gated).
    /// TODO(spec §5.3).
    /// </summary>
    public ToolKind Classify(string toolName) => throw new NotImplementedException();

    public bool IsWrite(string toolName) => Classify(toolName) == ToolKind.Write;

    /// <summary>Invoke a tool and return its (possibly truncated) result. TODO(spec §5.2).</summary>
    public Task<ToolResult> CallToolAsync(ToolCall call, CancellationToken ct) =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
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
