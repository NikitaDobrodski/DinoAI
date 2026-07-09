namespace DinoAI.Core.Sessions;

public interface IAgentSessionStore
{
    Task<IReadOnlyList<AgentSession>> ListAsync(CancellationToken cancellationToken = default);

    Task<AgentSession> CreateAsync(string? title = null, CancellationToken cancellationToken = default);

    Task<AgentSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<AgentSession> AddMessageAsync(
        Guid sessionId,
        AgentMessageRole role,
        string content,
        CancellationToken cancellationToken = default);
}

