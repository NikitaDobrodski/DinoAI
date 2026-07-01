using System.Text.Json;

namespace DinoAI.Core.Sessions;

public sealed class FileAgentSessionStore : IAgentSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAgentSessionStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Session store path cannot be empty.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<AgentSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            return state.Sessions
                .OrderByDescending(session => session.UpdatedAt)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AgentSession> CreateAsync(string? title = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var session = new AgentSession(
                Guid.NewGuid(),
                string.IsNullOrWhiteSpace(title) ? "New DinoAI session" : title.Trim(),
                now,
                now,
                []);

            state.Sessions.Add(session);
            await SaveAsync(state, cancellationToken);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AgentSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            return state.Sessions.FirstOrDefault(session => session.Id == sessionId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AgentSession> AddMessageAsync(
        Guid sessionId,
        AgentMessageRole role,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty.", nameof(content));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var index = state.Sessions.FindIndex(session => session.Id == sessionId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Session '{sessionId}' was not found.");
            }

            var current = state.Sessions[index];
            var messages = current.Messages
                .Append(new AgentMessage(Guid.NewGuid(), role, content.Trim(), DateTimeOffset.UtcNow))
                .ToArray();

            var updated = current with
            {
                UpdatedAt = DateTimeOffset.UtcNow,
                Messages = messages
            };

            state.Sessions[index] = updated;
            await SaveAsync(state, cancellationToken);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SessionStoreState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new SessionStoreState();
        }

        await using var stream = File.OpenRead(_filePath);
        var state = await JsonSerializer.DeserializeAsync<SessionStoreState>(stream, JsonOptions, cancellationToken);
        return state ?? new SessionStoreState();
    }

    private async Task SaveAsync(SessionStoreState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private sealed class SessionStoreState
    {
        public List<AgentSession> Sessions { get; set; } = [];
    }
}
