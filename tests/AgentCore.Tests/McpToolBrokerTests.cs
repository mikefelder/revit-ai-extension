using Xunit;

namespace AgentCore.Tests;

/// <summary>
/// Placeholder test class (spec §7 Phase 1). Real cases: schema conversion, classification
/// defaults (unknown => Write), loop rejection handling, truncation, max-iteration guard.
/// </summary>
public class McpToolBrokerTests
{
    [Fact(Skip = "Scaffold only — implemented in Phase 1 (spec §7).")]
    public void UnknownTool_DefaultsToWrite()
    {
        // TODO(spec §5.3): assert McpToolBroker.Classify(unknownName) == ToolKind.Write.
    }
}
