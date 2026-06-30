namespace DinoAI.Core.Tools;

public sealed record AgentToolParameter(
    string Name,
    string Description,
    bool IsRequired,
    string? DefaultValue = null);
