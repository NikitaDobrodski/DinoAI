namespace DinoAI.Core.Tools;

public sealed record AgentToolResult(
    bool IsSuccess,
    object? Data,
    string? Error)
{
    public static AgentToolResult Success(object? data) => new(true, data, null);

    public static AgentToolResult Failure(string error) => new(false, null, error);
}
