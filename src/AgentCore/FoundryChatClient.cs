using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using OAI = OpenAI.Chat;

namespace AgentCore;

/// <summary>
/// Streaming chat client abstraction so the agent loop can be tested with a mock model (spec §7).
/// </summary>
public interface IFoundryChatClient
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolSchema> tools,
        CancellationToken ct);
}

/// <summary>
/// Streaming chat-completions client against the Azure AI Foundry model deployment
/// (spec §2.1 D5, §5.2). Uses the OpenAI-compatible endpoint so the deployed model
/// (GPT-4.1 / GPT-5-class / Claude via the Foundry catalog) is swappable without code changes.
/// </summary>
public sealed class FoundryChatClient : IFoundryChatClient
{
    private readonly FoundryConfig _config;
    private readonly OAI.ChatClient _chatClient;

    public FoundryChatClient(FoundryConfig config)
    {
        _config = config;
        var endpoint = new Uri(config.Endpoint);

        var azureClient = config.Auth == FoundryAuthMode.Entra
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(ReadApiKey(config)));

        _chatClient = azureClient.GetChatClient(config.Deployment);
    }

    private static string ReadApiKey(FoundryConfig config)
    {
        var envVar = config.ApiKeyEnvVar
            ?? throw new InvalidOperationException("foundry.apiKeyEnvVar is required for apiKey auth.");
        var key = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException(
                $"Foundry API key not found in environment variable '{envVar}'.");
        return key;
    }

    /// <summary>
    /// Stream a chat completion with tool calling. Yields text deltas as they arrive; the final
    /// event carries any requested tool calls (spec §5.2, Phase 1).
    /// </summary>
    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolSchema> tools,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var options = new OAI.ChatCompletionOptions
        {
            Temperature = _config.Temperature,
            MaxOutputTokenCount = _config.MaxOutputTokens,
        };
        foreach (var tool in tools)
        {
            options.Tools.Add(OAI.ChatTool.CreateFunctionTool(
                tool.Name,
                tool.Description,
                BinaryData.FromString(tool.ParametersJsonSchema)));
        }

        var oaiMessages = messages.Select(ToOpenAiMessage).ToList();
        var builders = new Dictionary<int, ToolCallBuilder>();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(oaiMessages, options, ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return new ChatStreamEvent.TextDelta(part.Text);
            }

            foreach (var tcu in update.ToolCallUpdates)
            {
                if (!builders.TryGetValue(tcu.Index, out var builder))
                {
                    builder = new ToolCallBuilder();
                    builders[tcu.Index] = builder;
                }

                if (!string.IsNullOrEmpty(tcu.ToolCallId))
                    builder.Id = tcu.ToolCallId;
                if (!string.IsNullOrEmpty(tcu.FunctionName))
                    builder.Name = tcu.FunctionName;
                if (tcu.FunctionArgumentsUpdate is not null)
                    builder.Arguments.Append(tcu.FunctionArgumentsUpdate.ToString());
            }
        }

        var toolCalls = builders
            .OrderBy(kv => kv.Key)
            .Select(kv => new ToolCall(
                kv.Value.Id ?? string.Empty,
                kv.Value.Name ?? string.Empty,
                kv.Value.Arguments.ToString()))
            .ToList();

        yield return new ChatStreamEvent.Completed(toolCalls);
    }

    private static OAI.ChatMessage ToOpenAiMessage(ChatMessage m) => m.Role switch
    {
        ChatRole.System => new OAI.SystemChatMessage(m.Content),
        ChatRole.User => new OAI.UserChatMessage(m.Content),
        ChatRole.Assistant => ToAssistantMessage(m),
        ChatRole.Tool => new OAI.ToolChatMessage(
            m.ToolCallId ?? throw new InvalidOperationException("Tool message missing ToolCallId."),
            m.Content),
        _ => throw new ArgumentOutOfRangeException(nameof(m), m.Role, "Unknown chat role."),
    };

    private static OAI.AssistantChatMessage ToAssistantMessage(ChatMessage m)
    {
        if (m.ToolCalls is { Count: > 0 })
        {
            var calls = m.ToolCalls.Select(tc => OAI.ChatToolCall.CreateFunctionToolCall(
                tc.Id, tc.Name, BinaryData.FromString(tc.ArgumentsJson)));
            var message = new OAI.AssistantChatMessage(calls);
            if (!string.IsNullOrEmpty(m.Content))
                message.Content.Add(OAI.ChatMessageContentPart.CreateTextPart(m.Content));
            return message;
        }

        return new OAI.AssistantChatMessage(m.Content);
    }

    private sealed class ToolCallBuilder
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder Arguments { get; } = new();
    }
}

/// <summary>An incremental event from the streaming model call.</summary>
public abstract record ChatStreamEvent
{
    /// <summary>A streamed text delta to append to the assistant message.</summary>
    public sealed record TextDelta(string Text) : ChatStreamEvent;

    /// <summary>The turn completed; carries any tool calls the model requested.</summary>
    public sealed record Completed(IReadOnlyList<ToolCall> ToolCalls) : ChatStreamEvent;
}
