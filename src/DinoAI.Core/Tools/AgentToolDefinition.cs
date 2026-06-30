namespace DinoAI.Core.Tools;

public sealed record AgentToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<AgentToolParameter> Parameters);
