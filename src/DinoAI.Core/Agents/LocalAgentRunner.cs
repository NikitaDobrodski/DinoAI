using DinoAI.Core.Sessions;
using DinoAI.Core.Shell;
using DinoAI.Core.Tools;
using DinoAI.Core.Workspace;

namespace DinoAI.Core.Agents;

public sealed class LocalAgentRunner(
    IAgentSessionStore sessions,
    IAgentToolRegistry tools) : IAgentRunner
{
    public async Task<AgentTurnResult> RunAsync(
        Guid sessionId,
        string workspaceRoot,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new ArgumentException("User message cannot be empty.", nameof(userMessage));
        }

        var session = await sessions.AddMessageAsync(sessionId, AgentMessageRole.User, userMessage, cancellationToken);
        var plans = Plan(userMessage);
        var calls = new List<AgentToolCall>();

        if (plans.Count == 0)
        {
            session = await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Assistant,
                "I can inspect and check this workspace now. Try /workspace, /files *.csproj, /read README.md, /build, /status, or /diff.",
                cancellationToken);

            return new AgentTurnResult(session, calls);
        }

        foreach (var plan in plans)
        {
            var result = await tools.ExecuteAsync(
                plan.ToolName,
                new AgentToolContext(workspaceRoot, plan.Arguments),
                cancellationToken);

            calls.Add(new AgentToolCall(plan.ToolName, plan.Arguments, result));
            session = await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Tool,
                FormatToolMessage(plan.ToolName, result),
                cancellationToken);
        }

        session = await sessions.AddMessageAsync(
            sessionId,
            AgentMessageRole.Assistant,
            FormatAssistantMessage(calls),
            cancellationToken);

        return new AgentTurnResult(session, calls);
    }

    private static IReadOnlyList<ToolPlan> Plan(string userMessage)
    {
        var trimmed = userMessage.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower is "/build" or "build")
        {
            return [new ToolPlan("shell.run", new Dictionary<string, string?>
            {
                ["command"] = "dotnet build DinoAI.slnx",
                ["timeoutSeconds"] = "120"
            })];
        }

        if (lower is "/test" or "test")
        {
            return [new ToolPlan("shell.run", new Dictionary<string, string?>
            {
                ["command"] = "dotnet test DinoAI.slnx",
                ["timeoutSeconds"] = "120"
            })];
        }

        if (lower is "/status" or "status" or "git status")
        {
            return [new ToolPlan("shell.run", new Dictionary<string, string?>
            {
                ["command"] = "git status --short"
            })];
        }

        if (lower is "/diff" or "diff" or "git diff")
        {
            return [new ToolPlan("shell.run", new Dictionary<string, string?>
            {
                ["command"] = "git diff --stat"
            })];
        }

        if (lower is "/workspace" or "workspace")
        {
            return [new ToolPlan("workspace.describe", new Dictionary<string, string?>())];
        }

        if (lower.StartsWith("/files", StringComparison.Ordinal))
        {
            var pattern = trimmed.Length > "/files".Length
                ? trimmed["/files".Length..].Trim()
                : "*";

            return [new ToolPlan("workspace.find_files", new Dictionary<string, string?>
            {
                ["pattern"] = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern,
                ["maxResults"] = "50"
            })];
        }

        if (lower.StartsWith("/read ", StringComparison.Ordinal))
        {
            return [new ToolPlan("workspace.read_file", new Dictionary<string, string?>
            {
                ["path"] = trimmed["/read ".Length..].Trim(),
                ["maxBytes"] = "8192"
            })];
        }

        if (lower.Contains("build", StringComparison.Ordinal))
        {
            return [new ToolPlan("shell.run", new Dictionary<string, string?>
            {
                ["command"] = "dotnet build DinoAI.slnx",
                ["timeoutSeconds"] = "120"
            })];
        }

        if (lower.Contains("csproj", StringComparison.Ordinal) || lower.Contains("project file", StringComparison.Ordinal))
        {
            return [new ToolPlan("workspace.find_files", new Dictionary<string, string?>
            {
                ["pattern"] = "*.csproj",
                ["maxResults"] = "50"
            })];
        }

        if (lower.Contains("readme", StringComparison.Ordinal))
        {
            return [new ToolPlan("workspace.read_file", new Dictionary<string, string?>
            {
                ["path"] = "README.md",
                ["maxBytes"] = "8192"
            })];
        }

        if (lower.Contains("workspace", StringComparison.Ordinal) || lower.Contains("structure", StringComparison.Ordinal))
        {
            return [new ToolPlan("workspace.describe", new Dictionary<string, string?>())];
        }

        return [];
    }

    private static string FormatToolMessage(string toolName, AgentToolResult result)
    {
        if (!result.IsSuccess)
        {
            return $"{toolName} failed: {result.Error}";
        }

        return result.Data switch
        {
            WorkspaceInfo info => $"{toolName}: {info.TopLevelDirectories.Count} directories, {info.TopLevelFiles.Count} files at {info.RootPath}.",
            IReadOnlyList<WorkspaceFile> files => $"{toolName}: found {files.Count} file(s).",
            WorkspaceFileContent file => $"{toolName}: read {file.RelativePath} ({file.SizeBytes} bytes, truncated: {file.WasTruncated}).",
            WorkspaceWriteResult write => $"{toolName}: wrote {write.RelativePath} ({write.SizeBytes} bytes, created: {write.WasCreated}, overwritten: {write.WasOverwritten}).",
            ShellCommandResult shell => $"{toolName}: exit {shell.ExitCode} in {shell.Duration.TotalSeconds:F1}s (timeout: {shell.TimedOut}).",
            _ => $"{toolName}: completed."
        };
    }

    private static string FormatAssistantMessage(IReadOnlyList<AgentToolCall> calls)
    {
        var successfulCalls = calls.Where(call => call.Result.IsSuccess).ToArray();
        if (successfulCalls.Length == 0)
        {
            return "I tried to use a tool, but the call failed. Check the tool message above for details.";
        }

        var last = successfulCalls[^1];
        return last.Result.Data switch
        {
            WorkspaceInfo info => FormatWorkspaceInfo(info),
            IReadOnlyList<WorkspaceFile> files => FormatFiles(files),
            WorkspaceFileContent file => FormatFileContent(file),
            WorkspaceWriteResult write => $"I wrote {write.RelativePath} ({write.SizeBytes} bytes). Created: {write.WasCreated}. Overwritten: {write.WasOverwritten}.",
            ShellCommandResult shell => FormatShellResult(shell),
            _ => "Done. I executed the planned tool call."
        };
    }

    private static string FormatWorkspaceInfo(WorkspaceInfo info)
    {
        var directories = info.TopLevelDirectories.Count == 0
            ? "no top-level directories"
            : string.Join(", ", info.TopLevelDirectories.Select(directory => directory + "/"));

        var files = info.TopLevelFiles.Count == 0
            ? "no top-level files"
            : string.Join(", ", info.TopLevelFiles);

        return $"Workspace {info.RootPath} contains {directories}. Top-level files: {files}.";
    }

    private static string FormatFiles(IReadOnlyList<WorkspaceFile> files)
    {
        if (files.Count == 0)
        {
            return "I did not find matching files in this workspace.";
        }

        var preview = string.Join(Environment.NewLine, files.Take(12).Select(file => $"- {file.RelativePath} ({file.SizeBytes} bytes)"));
        var suffix = files.Count > 12 ? Environment.NewLine + $"...and {files.Count - 12} more." : string.Empty;
        return $"I found {files.Count} matching file(s):{Environment.NewLine}{preview}{suffix}";
    }

    private static string FormatShellResult(ShellCommandResult result)
    {
        var output = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput;
        var trimmed = output.Trim();
        var preview = trimmed.Length > 2000 ? trimmed[..2000] + Environment.NewLine + "[truncated]" : trimmed;
        return $"Command `{result.Command}` exited with code {result.ExitCode} in {result.Duration.TotalSeconds:F1}s." +
               (string.IsNullOrWhiteSpace(preview) ? string.Empty : Environment.NewLine + preview);
    }

    private static string FormatFileContent(WorkspaceFileContent file)
    {
        var suffix = file.WasTruncated ? Environment.NewLine + "[truncated]" : string.Empty;
        return $"Here is {file.RelativePath}:{Environment.NewLine}{file.Content}{suffix}";
    }

    private sealed record ToolPlan(string ToolName, IReadOnlyDictionary<string, string?> Arguments);
}
