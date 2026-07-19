using Xunit;

namespace AgentCore.Tests;

/// <summary>Config binding (spec §6), transcript persistence, and context rendering (spec §5.4).</summary>
public class ConfigAndStateTests
{
    [Fact]
    public void Load_ValidConfig_BindsAllSections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cfg-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
        {
          "foundry": { "endpoint": "https://x.openai.azure.com/", "deployment": "gpt",
                       "auth": "apiKey", "apiKeyEnvVar": "K", "maxOutputTokens": 100, "temperature": 0.1 },
          "mcp": { "serverCommand": "node", "serverArgs": ["a.js"], "startupTimeoutSeconds": 5 },
          "tools": { "readAllowlist": [], "writeEnabled": ["rename_views"], "resultCharCap": 100 },
          "revit": { "targetVersion": "2026" },
          "logging": { "level": "Information" }
        }
        """);

        try
        {
            var config = AppConfig.Load(path);

            Assert.Equal("gpt", config.Foundry.Deployment);
            Assert.Equal(FoundryAuthMode.ApiKey, config.Foundry.Auth);
            Assert.Contains("rename_views", config.Tools.WriteEnabled);
            Assert.Equal("2026", config.Revit.TargetVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            AppConfig.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")));
    }

    [Fact]
    public async Task Transcript_SaveAndLoad_RoundTrips()
    {
        var transcript = new Transcript();
        transcript.Add(new ChatMessage(ChatRole.System, "sys"));
        transcript.Add(new ChatMessage(ChatRole.User, "hello"));
        transcript.Add(new ChatMessage(ChatRole.Assistant, "hi",
            ToolCalls: [new ToolCall("c1", "get_walls", "{}")]));

        var path = Path.Combine(Path.GetTempPath(), $"tr-{Guid.NewGuid():N}.json");
        try
        {
            await transcript.SaveAsync(path, CancellationToken.None);
            var loaded = await Transcript.LoadAsync(path, CancellationToken.None);

            Assert.Equal(3, loaded.Messages.Count);
            Assert.Equal(ChatRole.Assistant, loaded.Messages[2].Role);
            Assert.Equal("get_walls", loaded.Messages[2].ToolCalls![0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToPromptBlock_ContainsKeyFields()
    {
        var snapshot = new RevitContextSnapshot(
            "Tower.rvt", @"C:\models\Tower.rvt", "2026", "Millimeters",
            "Level 2", "FloorPlan", "L2", 1,
            [new SelectedElement(1234, "Doors", "M_Single-Flush")]);

        var block = snapshot.ToPromptBlock();

        Assert.Contains("Tower.rvt", block);
        Assert.Contains("revitVersion: 2026", block);
        Assert.Contains("selectionCount: 1", block);
        Assert.Contains("Doors", block);
    }
}
