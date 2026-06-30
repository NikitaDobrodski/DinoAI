namespace DinoAI.Core.Shell;

public interface IShellCommandRunner
{
    Task<ShellCommandResult> RunAsync(
        string workingDirectory,
        string command,
        int timeoutSeconds = 60,
        CancellationToken cancellationToken = default);
}
