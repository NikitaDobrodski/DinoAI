namespace DinoAI.Core.Models;

public sealed record OpenAICompatibleChatModelOptions(
    string BaseUrl,
    string? ApiKey,
    string Model)
{
    public static OpenAICompatibleChatModelOptions FromEnvironment()
    {
        return new OpenAICompatibleChatModelOptions(
            Environment.GetEnvironmentVariable("DINOAI_OPENAI_BASE_URL") ?? "https://api.openai.com/v1",
            Environment.GetEnvironmentVariable("DINOAI_OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("DINOAI_OPENAI_MODEL") ?? "gpt-4.1-mini");
    }
}
