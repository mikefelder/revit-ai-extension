using Xunit;

namespace AgentCore.Tests;

/// <summary>
/// Classification and truncation rules for <see cref="McpToolBroker"/> (spec §5.3, §5.2).
/// </summary>
public class McpToolBrokerTests
{
    private static McpToolBroker Broker(ToolsConfig tools) =>
        new(new McpConfig { ServerCommand = "node", ServerArgs = ["x.js"] }, tools);

    [Fact]
    public void UnknownTool_DefaultsToWrite()
    {
        var broker = Broker(new ToolsConfig());
        Assert.Equal(ToolKind.Write, broker.Classify("do_something_weird"));
    }

    [Theory]
    [InlineData("get_walls")]
    [InlineData("list_levels")]
    [InlineData("query_warnings")]
    [InlineData("export_schedule")]
    public void ReadPrefixes_ClassifyAsRead(string name)
    {
        var broker = Broker(new ToolsConfig());
        Assert.Equal(ToolKind.Read, broker.Classify(name));
    }

    [Fact]
    public void ReadAllowlist_ForcesRead()
    {
        var broker = Broker(new ToolsConfig { ReadAllowlist = ["do_something_weird"] });
        Assert.Equal(ToolKind.Read, broker.Classify("do_something_weird"));
    }

    [Fact]
    public void WriteEnabled_ClassifiesAsWrite()
    {
        var broker = Broker(new ToolsConfig { WriteEnabled = ["rename_views"] });
        Assert.Equal(ToolKind.Write, broker.Classify("rename_views"));
    }

    [Fact]
    public void EmptyName_DefaultsToWrite()
    {
        var broker = Broker(new ToolsConfig());
        Assert.Equal(ToolKind.Write, broker.Classify(""));
    }

    [Fact]
    public void Truncate_UnderCap_NotTruncated()
    {
        var result = McpToolBroker.Truncate("short", 100);
        Assert.False(result.Truncated);
        Assert.Equal("short", result.Content);
    }

    [Fact]
    public void Truncate_OverCap_AddsMarker()
    {
        var result = McpToolBroker.Truncate(new string('x', 200), 50);
        Assert.True(result.Truncated);
        Assert.Contains("[truncated", result.Content);
        Assert.StartsWith(new string('x', 50), result.Content);
    }
}
