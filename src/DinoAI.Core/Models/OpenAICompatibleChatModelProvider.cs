using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DinoAI.Core.Tools;

namespace DinoAI.Core.Models;

public sealed class OpenAICompatibleChatModelProvider(
    HttpClient httpClient,
    IChatModelSettingsStore settingsStore) : IChatModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        var tools = request.Tools?.Select(ToChatCompletionTool).ToArray();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        httpRequest.Content = JsonContent.Create(new ChatCompletionRequest(
            request.Model ?? options.Model,
            request.Messages.Select(ToChatCompletionMessage).ToArray(),
            request.Temperature,
            tools is { Length: > 0 } ? tools : null,
            tools is { Length: > 0 } ? "auto" : null), options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await FormatProviderErrorAsync(response, cancellationToken));
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var message = payload?.Choices?.FirstOrDefault()?.Message;
        var toolCalls = message?.ToolCalls?.Select(ToModelToolCall).Where(call => call is not null).Cast<ChatModelToolCall>().ToArray();
        var content = message?.Content;

        if (string.IsNullOrWhiteSpace(content) && toolCalls is not { Length: > 0 })
        {
            throw new InvalidOperationException("Ответ модели не содержит текста ассистента или вызовов инструментов.");
        }

        return new ChatModelResponse(content, payload?.Model ?? request.Model ?? options.Model, toolCalls);
    }

    private static ChatCompletionMessage ToChatCompletionMessage(ChatModelMessage message)
    {
        return new ChatCompletionMessage(
            message.Role,
            message.Content,
            message.ToolCalls?.Select(call => new ChatCompletionToolCall(
                call.Id,
                "function",
                new ChatCompletionFunctionCall(call.Name, JsonSerializer.Serialize(call.Arguments)))).ToArray(),
            message.ToolCallId,
            message.Name);
    }

    private static ChatCompletionTool ToChatCompletionTool(AgentToolDefinition definition)
    {
        var properties = definition.Parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => new ChatCompletionToolParameter("string", parameter.Description),
            StringComparer.OrdinalIgnoreCase);

        return new ChatCompletionTool(
            "function",
            new ChatCompletionFunctionTool(
                ToFunctionName(definition.Name),
                $"{definition.Description} Internal tool name: {definition.Name}.",
                new ChatCompletionToolParameters(
                    "object",
                    properties,
                    definition.Parameters.Where(parameter => parameter.IsRequired).Select(parameter => parameter.Name).ToArray())));
    }

    private static ChatModelToolCall? ToModelToolCall(ChatCompletionToolCall call)
    {
        if (!string.Equals(call.Type, "function", StringComparison.OrdinalIgnoreCase) || call.Function is null)
        {
            return null;
        }

        return new ChatModelToolCall(call.Id, FromFunctionName(call.Function.Name), ParseArguments(call.Function.Arguments));
    }

    private static string ToFunctionName(string toolName)
    {
        return toolName.Replace(".", "__", StringComparison.Ordinal);
    }

    private static string FromFunctionName(string functionName)
    {
        return functionName.Replace("__", ".", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string?> ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            var arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null => null,
                    JsonValueKind.Undefined => null,
                    _ => property.Value.GetRawText()
                };
            }

            return arguments;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
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
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("tools")] IReadOnlyList<ChatCompletionTool>? Tools,
        [property: JsonPropertyName("tool_choice")] string? ToolChoice);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls")] IReadOnlyList<ChatCompletionToolCall>? ToolCalls = null,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null,
        [property: JsonPropertyName("name")] string? Name = null);

    private sealed record ChatCompletionTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] ChatCompletionFunctionTool Function);

    private sealed record ChatCompletionFunctionTool(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] ChatCompletionToolParameters Parameters);

    private sealed record ChatCompletionToolParameters(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, ChatCompletionToolParameter> Properties,
        [property: JsonPropertyName("required")] IReadOnlyList<string> Required);

    private sealed record ChatCompletionToolParameter(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("description")] string Description);

    private sealed record ChatCompletionToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] ChatCompletionFunctionCall? Function);

    private sealed record ChatCompletionFunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string? Arguments);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message);
}
