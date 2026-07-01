using DinoAI.Core.Models;
using DinoAI.Core.Sessions;
using DinoAI.Core.Shell;
using DinoAI.Core.Tools;
using DinoAI.Core.Workspace;

namespace DinoAI.Core.Agents;

public sealed class LocalAgentRunner(
    IAgentSessionStore sessions,
    IAgentToolRegistry tools,
    IChatModelProvider modelProvider) : IAgentRunner
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
            session = await CompleteWithModelAsync(sessionId, session, cancellationToken);

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

    private async Task<AgentSession> CompleteWithModelAsync(
        Guid sessionId,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        var status = modelProvider.GetStatus();
        if (!status.IsConfigured)
        {
            return await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Assistant,
                "Провайдер модели пока не настроен. Я всё равно могу проверять workspace локальными командами: попробуй /workspace, /files *.csproj, /read README.md, /build, /status или /diff. Для DeepSeek/OpenRouter и других OpenAI-compatible API задай DINOAI_OPENAI_BASE_URL, DINOAI_OPENAI_API_KEY и DINOAI_OPENAI_MODEL.",
                cancellationToken);
        }

        try
        {
            var messages = session.Messages
                .Where(message => message.Role is AgentMessageRole.User or AgentMessageRole.Assistant or AgentMessageRole.System)
                .TakeLast(20)
                .Select(message => new ChatModelMessage(ToModelRole(message.Role), message.Content))
                .Prepend(new ChatModelMessage("system", "Ты DinoAI, локальный C# coding agent. Отвечай кратко и практично. Если нужен доступ к файлам или shell, предлагай безопасные команды workspace-инструментов."))
                .ToArray();

            var response = await modelProvider.CompleteAsync(new ChatModelRequest(messages), cancellationToken);
            return await sessions.AddMessageAsync(sessionId, AgentMessageRole.Assistant, response.Content, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Assistant,
                $"Запрос к модели не удался: {ex.Message}",
                cancellationToken);
        }
    }

    private static string ToModelRole(AgentMessageRole role)
    {
        return role switch
        {
            AgentMessageRole.User => "user",
            AgentMessageRole.Assistant => "assistant",
            AgentMessageRole.System => "system",
            _ => "user"
        };
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
            return $"{toolName}: ошибка: {result.Error}";
        }

        return result.Data switch
        {
            WorkspaceInfo info => $"{toolName}: {info.TopLevelDirectories.Count} directories, {info.TopLevelFiles.Count} files at {info.RootPath}.",
            IReadOnlyList<WorkspaceFile> files => $"{toolName}: найдено файлов: {files.Count}.",
            WorkspaceFileContent file => $"{toolName}: прочитан файл {file.RelativePath} ({file.SizeBytes} байт, обрезан: {file.WasTruncated}).",
            WorkspaceWriteResult write => $"{toolName}: записан файл {write.RelativePath} ({write.SizeBytes} байт, создан: {write.WasCreated}, перезаписан: {write.WasOverwritten}).",
            ShellCommandResult shell => $"{toolName}: код выхода {shell.ExitCode}, время {shell.Duration.TotalSeconds:F1}с, таймаут: {shell.TimedOut}.",
            _ => $"{toolName}: completed."
        };
    }

    private static string FormatAssistantMessage(IReadOnlyList<AgentToolCall> calls)
    {
        var successfulCalls = calls.Where(call => call.Result.IsSuccess).ToArray();
        if (successfulCalls.Length == 0)
        {
            return "Я попытался выполнить инструмент, но вызов завершился ошибкой. Подробности выше в сообщении инструмента.";
        }

        var last = successfulCalls[^1];
        return last.Result.Data switch
        {
            WorkspaceInfo info => FormatWorkspaceInfo(info),
            IReadOnlyList<WorkspaceFile> files => FormatFiles(files),
            WorkspaceFileContent file => FormatFileContent(file),
            WorkspaceWriteResult write => $"Я записал {write.RelativePath} ({write.SizeBytes} байт). Создан: {write.WasCreated}. Перезаписан: {write.WasOverwritten}.",
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
            return "Я не нашёл подходящих файлов в этом workspace.";
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
        return $"Команда `{result.Command}` завершилась с кодом {result.ExitCode} за {result.Duration.TotalSeconds:F1}с." +
               (string.IsNullOrWhiteSpace(preview) ? string.Empty : Environment.NewLine + preview);
    }

    private static string FormatFileContent(WorkspaceFileContent file)
    {
        var suffix = file.WasTruncated ? Environment.NewLine + "[truncated]" : string.Empty;
        return $"Here is {file.RelativePath}:{Environment.NewLine}{file.Content}{suffix}";
    }

    private sealed record ToolPlan(string ToolName, IReadOnlyDictionary<string, string?> Arguments);
}



