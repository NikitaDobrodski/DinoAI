namespace DinoAI.Core.Sessions;

public sealed record AgentMessage(
    Guid Id,
    AgentMessageRole Role,
    string Content,
    DateTimeOffset CreatedAt);
