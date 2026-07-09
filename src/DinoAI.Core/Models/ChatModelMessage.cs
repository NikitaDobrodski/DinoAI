namespace DinoAI.Core.Models;

public sealed record ChatModelMessage(
    string Role,
    string? Content,
    IReadOnlyList<ChatModelToolCall>? ToolCalls = null,
    string? ToolCallId = null,
    string? Name = null);

public sealed record ChatModelToolCall(
    string Id,
    string Name,
    IReadOnlyDictionary<string, string?> Arguments);
