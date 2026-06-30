namespace DinoAI.Core.Sessions;

public sealed record AgentSession(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AgentMessage> Messages);
