using System.Diagnostics;

namespace DinoAI.Core.Shell;

public sealed class ProcessShellCommandRunner : IShellCommandRunner
{
    public async Task<ShellCommandResult> RunAsync(
        string workingDirectory,
        string command,
        int timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Shell command cannot be empty.", nameof(command));
        }

        var root = NormalizeExistingRoot(workingDirectory);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600));
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add(command);
        }

        using var process = new Process { StartInfo = startInfo };
        var startedAt = DateTimeOffset.UtcNow;
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var exited = await WaitForExitAsync(process, timeout, timeoutCts.Token);

        if (!exited)
        {
            TryKill(process);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var duration = DateTimeOffset.UtcNow - startedAt;
        var exitCode = exited ? process.ExitCode : -1;

        return new ShellCommandResult(command, root, exitCode, stdout, stderr, duration, !exited);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var exitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(exitTask, delayTask);
        return completed == exitTask;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string NormalizeExistingRoot(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
        }

        var root = Path.GetFullPath(workingDirectory.Trim());
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Working directory '{root}' was not found.");
        }

        return root;
    }
}
