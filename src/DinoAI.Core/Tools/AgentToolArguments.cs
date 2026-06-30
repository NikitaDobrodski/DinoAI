namespace DinoAI.Core.Tools;

public static class AgentToolArguments
{
    public static string GetString(
        this IReadOnlyDictionary<string, string?> arguments,
        string name,
        string defaultValue)
    {
        return arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;
    }

    public static int GetInt32(
        this IReadOnlyDictionary<string, string?> arguments,
        string name,
        int defaultValue)
    {
        if (!arguments.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static bool GetBoolean(
        this IReadOnlyDictionary<string, string?> arguments,
        string name,
        bool defaultValue)
    {
        if (!arguments.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
