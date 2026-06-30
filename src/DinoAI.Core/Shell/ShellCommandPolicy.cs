namespace DinoAI.Core.Shell;

public static class ShellCommandPolicy
{
    private static readonly string[] AllowedPrefixes =
    [
        "dotnet build",
        "dotnet test",
        "dotnet --info",
        "git status",
        "git diff",
        "git log"
    ];

    public static bool IsAllowedWithoutApproval(string command)
    {
        var normalized = Normalize(command);
        return AllowedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string command)
    {
        return string.Join(' ', command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
