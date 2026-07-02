using System.Net;
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
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await FormatProviderErrorAsync(response, cancellationToken));
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ответ модели не содержит текста ассистента.");
        }

        return new ChatModelResponse(content, payload?.Model ?? request.Model ?? options.Model);
    }

    private static async Task<string> FormatProviderErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var details = await ReadErrorDetailsAsync(response, cancellationToken);
        var reason = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "ключ API не принят провайдером",
            HttpStatusCode.Forbidden => "у ключа API нет доступа к этой модели или endpoint",
            HttpStatusCode.PaymentRequired => "у API-аккаунта нет доступного баланса или бесплатного лимита",
            HttpStatusCode.NotFound => "модель или endpoint не найдены",
            (HttpStatusCode)429 => "превышен лимит запросов",
            _ => $"провайдер вернул HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
        };

        return string.IsNullOrWhiteSpace(details)
            ? $"Проверка не прошла: {reason}."
            : $"Проверка не прошла: {reason}. Детали: {details}";
    }

    private static async Task<string?> ReadErrorDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return content.Length <= 500 ? content : content[..500] + "...";
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }
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
