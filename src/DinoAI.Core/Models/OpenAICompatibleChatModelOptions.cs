namespace DinoAI.Core.Models;

public sealed record OpenAICompatibleChatModelOptions(
    string BaseUrl,
    string? ApiKey,
    string Model)
{
    public const string GroqBaseUrl = "https://api.groq.com/openai/v1";
    public const string GroqDefaultModel = "qwen/qwen3-32b";

    public static OpenAICompatibleChatModelOptions FromEnvironment()
    {
        var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        var explicitBaseUrl = Environment.GetEnvironmentVariable("DINOAI_OPENAI_BASE_URL");
        var explicitApiKey = Environment.GetEnvironmentVariable("DINOAI_OPENAI_API_KEY");
        var explicitModel = Environment.GetEnvironmentVariable("DINOAI_OPENAI_MODEL");

        if (string.IsNullOrWhiteSpace(explicitBaseUrl)
            && string.IsNullOrWhiteSpace(explicitApiKey)
            && !string.IsNullOrWhiteSpace(groqApiKey))
        {
            return new OpenAICompatibleChatModelOptions(
                GroqBaseUrl,
                groqApiKey,
                explicitModel ?? GroqDefaultModel);
        }

        return new OpenAICompatibleChatModelOptions(
            explicitBaseUrl ?? "https://api.openai.com/v1",
            explicitApiKey,
            explicitModel ?? "gpt-4.1-mini");
    }
}
