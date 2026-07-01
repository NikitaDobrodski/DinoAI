namespace DinoAI.Core.Models;

public interface IChatModelProvider
{
    ChatModelProviderStatus GetStatus();

    Task<ChatModelResponse> CompleteAsync(
        ChatModelRequest request,
        CancellationToken cancellationToken = default);
}
