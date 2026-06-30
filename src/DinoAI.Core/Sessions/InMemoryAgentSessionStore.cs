using System.Collections.Concurrent;

namespace DinoAI.Core.Sessions;

public sealed class InMemoryAgentSessionStore : IAgentSessionStore
{
    private readonly ConcurrentDictionary<Guid, MutableAgentSession> _sessions = new();

    public Task<IReadOnlyList<AgentSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Values
            .OrderByDescending(session => session.UpdatedAt)
            .Select(ToSnapshot)
            .ToArray();

        return Task.FromResult<IReadOnlyList<AgentSession>>(sessions);
    }

    public Task<AgentSession> CreateAsync(string? title = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new MutableAgentSession(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(title) ? "New DinoAI session" : title.Trim(),
            now,
            now);

        _sessions[session.Id] = session;
        return Task.FromResult(ToSnapshot(session));
    }

    public Task<AgentSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? ToSnapshot(session) : null);
    }

    public Task<AgentSession> AddMessageAsync(
        Guid sessionId,
        AgentMessageRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.", nameof(content));
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Session '{sessionId}' was not found.");
        }

        lock (session.Messages)
        {
            session.Messages.Add(new AgentMessage(Guid.NewGuid(), role, content.Trim(), DateTimeOffset.UtcNow));
            session.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(ToSnapshot(session));
        }
    }

    private static AgentSession ToSnapshot(MutableAgentSession session)
    {
        lock (session.Messages)
        {
            return new AgentSession(
                session.Id,
                session.Title,
                session.CreatedAt,
                session.UpdatedAt,
                session.Messages.ToArray());
        }
    }

    private sealed class MutableAgentSession(
        Guid id,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        public Guid Id { get; } = id;
        public string Title { get; } = title;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public DateTimeOffset UpdatedAt { get; set; } = updatedAt;
        public List<AgentMessage> Messages { get; } = [];
    }
}
