using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore;

/// <summary>
/// Conversation state and persistence (spec §5.1, §5.2). Sessions are persisted to
/// <c>%LocalAppData%/RevitAssistant/sessions/*.json</c>.
/// </summary>
public sealed class Transcript
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<ChatMessage> _messages = [];

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public void Add(ChatMessage message) => _messages.Add(message);

    /// <summary>Persist the transcript to disk as JSON (spec §5.1).</summary>
    public async Task SaveAsync(string path, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, _messages, SerializerOptions, ct);
    }

    /// <summary>Load a persisted transcript from disk (spec §5.1).</summary>
    public static async Task<Transcript> LoadAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var messages = await JsonSerializer.DeserializeAsync<List<ChatMessage>>(
            stream, SerializerOptions, ct) ?? [];

        var transcript = new Transcript();
        transcript._messages.AddRange(messages);
        return transcript;
    }
}

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

/// <summary>A single entry in the conversation (user / assistant / tool event).</summary>
public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    string? ToolCallId = null);

/// <summary>A model-requested tool invocation.</summary>
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);
