namespace DinoAI.Core.Models;

public interface IChatModelSettingsStore
{
    OpenAICompatibleChatModelOptions Current { get; }

    Task<OpenAICompatibleChatModelOptions> GetAsync(CancellationToken cancellationToken = default);

    Task<OpenAICompatibleChatModelOptions> SaveAsync(
        OpenAICompatibleChatModelOptions options,
        CancellationToken cancellationToken = default);
}
