namespace DinoAI.Core.Shell;

public sealed record ShellCommandResult(
    string Command,
    string WorkingDirectory,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut);
