using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DinoAI.Core.Models;

public sealed class OpenAICompatibleChatModelProvider(
    HttpClient httpClient,
    IChatModelSettingsStore settingsStore) : IChatModelProvider
{
    public ChatModelProviderStatus GetStatus()
    {
        var options = settingsStore.Current;
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new ChatModelProviderStatus(
                "OpenAI-compatible",
                false,
                options.Model,
                "Укажи API key в настройках модели, чтобы включить ответы через ИИ.");
        }

        return new ChatModelProviderStatus("OpenAI-compatible", true, options.Model, null);
    }

    public async Task<ChatModelResponse> CompleteAsync(
        ChatModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsStore.GetAsync(cancellationToken);
        var status = GetStatus();
        if (!status.IsConfigured)
        {
            throw new InvalidOperationException(status.Reason ?? "Провайдер модели не настроен.");
        }

        var baseUrl = options.BaseUrl.TrimEnd('/');
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        httpRequest.Content = JsonContent.Create(new ChatCompletionRequest(
            request.Model ?? options.Model,
            request.Messages.Select(message => new ChatCompletionMessage(message.Role, message.Content)).ToArray(),
            request.Temperature));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ответ модели не содержит текста ассистента.");
        }

        return new ChatModelResponse(content, payload?.Model ?? request.Model ?? options.Model);
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message);
}
