namespace DinoAI.Core.Models;

/// <summary>
/// Чат-модель провайдера статуса, которая содержит информацию о том, настроен ли провайдер, его имя, модель и причину, если он не настроен.
/// </summary>
/// <param name="ProviderName"></param>
/// <param name="IsConfigured"></param>
/// <param name="Model"></param>
/// <param name="Reason"></param>
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
