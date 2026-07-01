namespace DinoAI.Core.Models;

public sealed record ChatModelProviderStatus(
    string ProviderName,
    bool IsConfigured,
    string? Model,
    string? Reason)
{
    public static ChatModelProviderStatus Ready(string providerName, string? model)
    {
        return new ChatModelProviderStatus(providerName, true, model, null);
    }

    public static ChatModelProviderStatus NotConfigured(string providerName, string reason, string? model = null)
    {
        return new ChatModelProviderStatus(providerName, false, model, reason);
    }
}
