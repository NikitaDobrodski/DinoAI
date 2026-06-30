namespace DinoAI.Core.Tools;

public sealed record AgentToolContext(
    string WorkspaceRoot,
    IReadOnlyDictionary<string, string?> Arguments,
    bool IsUserApproved = false);
