namespace DinoAI.Core.Models;

public sealed record ChatModelResponse(
    string? Content,
    string? Model,
    IReadOnlyList<ChatModelToolCall>? ToolCalls = null)
{
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}
