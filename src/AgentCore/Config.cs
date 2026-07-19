using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore;

/// <summary>
/// Strongly-typed application configuration (spec §6). Bound from the git-ignored
/// <c>config/appsettings.json</c>; a documented sample lives at
/// <c>config/appsettings.sample.json</c>. Secrets are supplied via environment variables or
/// Entra ID only — never stored in this object graph from source control.
/// </summary>
public sealed class AppConfig
{
    public required FoundryConfig Foundry { get; init; }
    public required McpConfig Mcp { get; init; }
    public required ToolsConfig Tools { get; init; }
    public required RevitConfig Revit { get; init; }
    public required LoggingConfig Logging { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Load and validate configuration from the given path (spec §6).</summary>
    public static AppConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Config path must be provided.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Configuration file not found: {path}", path);

        var json = File.ReadAllText(path);
        AppConfig config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions)
                ?? throw new InvalidOperationException($"Configuration file is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file is not valid JSON: {path}", ex);
        }

        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Foundry.Endpoint))
            throw new InvalidOperationException("foundry.endpoint is required.");
        if (string.IsNullOrWhiteSpace(Foundry.Deployment))
            throw new InvalidOperationException("foundry.deployment is required.");
        if (Foundry.Auth == FoundryAuthMode.ApiKey && string.IsNullOrWhiteSpace(Foundry.ApiKeyEnvVar))
            throw new InvalidOperationException(
                "foundry.apiKeyEnvVar is required when auth is 'apiKey'.");
        if (string.IsNullOrWhiteSpace(Mcp.ServerCommand))
            throw new InvalidOperationException("mcp.serverCommand is required.");
    }
}

public enum FoundryAuthMode
{
    ApiKey,
    Entra,
}

public sealed class FoundryConfig
{
    public required string Endpoint { get; init; }
    public required string Deployment { get; init; }
    public FoundryAuthMode Auth { get; init; } = FoundryAuthMode.ApiKey;
    public string? ApiKeyEnvVar { get; init; }
    public int MaxOutputTokens { get; init; } = 4096;
    public float Temperature { get; init; } = 0.2f;
}

public sealed class McpConfig
{
    public required string ServerCommand { get; init; }
    public required IReadOnlyList<string> ServerArgs { get; init; }
    public int StartupTimeoutSeconds { get; init; } = 20;
}

public sealed class ToolsConfig
{
    /// <summary>Tools forced to Read classification regardless of heuristics (spec §5.3).</summary>
    public IReadOnlyList<string> ReadAllowlist { get; init; } = [];

    /// <summary>The ONLY write tools offered to the model in v1 (spec §5.3).</summary>
    public IReadOnlyList<string> WriteEnabled { get; init; } = [];

    /// <summary>Tool results larger than this are truncated with a marker (spec §5.2).</summary>
    public int ResultCharCap { get; init; } = 20000;
}

public sealed class RevitConfig
{
    public string TargetVersion { get; init; } = "2026";
}

public sealed class LoggingConfig
{
    public string Level { get; init; } = "Information";
}
