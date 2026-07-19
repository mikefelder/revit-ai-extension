namespace AgentCore;

/// <summary>
/// Abstraction for the write-approval gate (spec §5.1, §5.2, D3). Write-classified tool calls
/// pause the agent loop and request a verdict; the UI renders an Approve/Reject card, while the
/// console harness prompts y/n. A rejection is returned to the model as a structured tool result
/// so it can continue gracefully.
/// </summary>
public interface IApprovalHandler
{
    Task<ApprovalVerdict> RequestAsync(ApprovalRequest request, CancellationToken ct);
}

/// <summary>A pending write tool call awaiting user approval.</summary>
public sealed record ApprovalRequest(
    string ToolName,
    string ArgumentsJson,
    string HumanSummary);

/// <summary>The user's decision on an <see cref="ApprovalRequest"/>.</summary>
public sealed record ApprovalVerdict(bool Approved, string? Reason = null);
