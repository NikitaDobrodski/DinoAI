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

    /// <summary>
    /// Агент выполняет один turn: получает сообщение от пользователя, планирует вызовы инструментов, выполняет их и формирует ответ ассистента.
    /// </summary>
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
            session = await CompleteWithModelAsync(sessionId, workspaceRoot, userMessage, session, calls, cancellationToken);

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

    /// <summary>
    /// Агент формирует ответ ассистента с помощью внешней модели, если она настроена. 
    /// Если модель не настроена, возвращается сообщение о том, что модель недоступна.
    /// </summary>
    private async Task<AgentSession> CompleteWithModelAsync(
        Guid sessionId,
        string workspaceRoot,
        string userMessage,
        AgentSession session,
        List<AgentToolCall> calls,
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
                .TakeLast(8)
                .Select(message => new ChatModelMessage(ToModelRole(message.Role), message.Content))
                .Prepend(new ChatModelMessage("system", BuildSystemPrompt(status)))
                .ToList();

            for (var iteration = 0; iteration < 4; iteration++)
            {
                var response = await modelProvider.CompleteAsync(
                    new ChatModelRequest(messages, Tools: GetModelTools(userMessage)),
                    cancellationToken);

                if (!response.HasToolCalls)
                {
                    var content = string.IsNullOrWhiteSpace(response.Content)
                        ? "Модель завершила ответ без текста."
                        : response.Content;

                    return await sessions.AddMessageAsync(sessionId, AgentMessageRole.Assistant, content, cancellationToken);
                }

                messages.Add(new ChatModelMessage("assistant", response.Content, response.ToolCalls));

                foreach (var toolCall in response.ToolCalls ?? [])
                {
                    var result = await tools.ExecuteAsync(
                        toolCall.Name,
                        new AgentToolContext(workspaceRoot, toolCall.Arguments),
                        cancellationToken);

                    calls.Add(new AgentToolCall(toolCall.Name, toolCall.Arguments, result));
                    session = await sessions.AddMessageAsync(
                        sessionId,
                        AgentMessageRole.Tool,
                        FormatToolMessage(toolCall.Name, result),
                        cancellationToken);

                    messages.Add(new ChatModelMessage(
                        "tool",
                        FormatToolResultForModel(toolCall.Name, result),
                        ToolCallId: toolCall.Id,
                        Name: toolCall.Name));
                }
            }

            return await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Assistant,
                "Модель несколько раз запрашивала инструменты подряд. Я остановил цикл, чтобы не выполнять лишние действия.",
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return await sessions.AddMessageAsync(
                sessionId,
                AgentMessageRole.Assistant,
                FormatModelFailure(ex),
                cancellationToken);
        }
    }

    private static string FormatModelFailure(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tokens per minute", StringComparison.OrdinalIgnoreCase)
            || message.Contains("429", StringComparison.OrdinalIgnoreCase)
            || message.Contains("превышен лимит", StringComparison.OrdinalIgnoreCase))
        {
            return "Groq временно упёрся в лимит токенов. Подожди примерно 30 секунд и повтори запрос. Я уже сократил историю и буду чаще использовать локальные workspace-команды, чтобы меньше жечь лимит.";
        }

        return $"Запрос к модели не удался: {message}";
    }

    /// <summary>
    /// Чат системное сообщение, которое сообщает пользователю, что DinoAI использует внешнюю модель с определённым Model ID. 
    /// Если модель не указана, используется "неизвестная модель". Сообщение также содержит инструкции по безопасному использованию инструментов workspace.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    private static string BuildSystemPrompt(ChatModelProviderStatus status)
    {
        var modelName = string.IsNullOrWhiteSpace(status.Model) ? "неизвестная модель" : status.Model;
        return $"Ты DinoAI, локальный C# agent-интерфейс для разработки. Твои ответы генерирует внешняя OpenAI-compatible модель с текущим Model ID: {modelName}. Если пользователь спрашивает, какая модель используется, называй именно этот Model ID. Отвечай кратко и практично. Для просмотра workspace используй workspace tools. Shell не используй для просмотра файлов: shell нужен только для явных команд build/test/git/status/diff или когда пользователь прямо просит выполнить shell-команду.";
    }

    private IReadOnlyList<AgentToolDefinition> GetModelTools(string userMessage)
    {
        var allowShell = ShouldExposeShellTool(userMessage);
        return tools.List()
            .Where(tool => allowShell || !tool.Name.Equals("shell.run", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool ShouldExposeShellTool(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();
        return lower.Contains("shell", StringComparison.Ordinal)
            || lower.Contains("терминал", StringComparison.Ordinal)
            || lower.Contains("команд", StringComparison.Ordinal)
            || lower.Contains("build", StringComparison.Ordinal)
            || lower.Contains("test", StringComparison.Ordinal)
            || lower.Contains("status", StringComparison.Ordinal)
            || lower.Contains("diff", StringComparison.Ordinal)
            || lower.Contains("git", StringComparison.Ordinal)
            || lower.StartsWith("/build", StringComparison.Ordinal)
            || lower.StartsWith("/test", StringComparison.Ordinal)
            || lower.StartsWith("/status", StringComparison.Ordinal)
            || lower.StartsWith("/diff", StringComparison.Ordinal);
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

    /// <summary>
    /// Планирование вызовов инструментов на основе сообщения пользователя. 
    /// Если сообщение содержит ключевые слова, такие как "build", "test", "status", "diff", "workspace", "files", "read", то возвращается соответствующий план вызова инструмента.
    /// </summary>
    /// <param name="userMessage"></param>
    /// <returns></returns>
    private static IReadOnlyList<ToolPlan> Plan(string userMessage)
    {
        var trimmed = userMessage.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower is "/help" or "/commands" or "/")
        {
            return [new ToolPlan("workspace.describe", new Dictionary<string, string?>())];
        }

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

        if (lower is "все" or "всё" or "all" or "/all")
        {
            return [new ToolPlan("workspace.find_files", new Dictionary<string, string?>
            {
                ["pattern"] = "*",
                ["maxResults"] = "200"
            })];
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

    /// <summary>
    /// Формат сообщения инструмента для добавления в сессию. Если инструмент завершился с ошибкой, возвращается сообщение об ошибке. 
    /// Если инструмент завершился успешно, возвращается краткое описание результата в зависимости от типа данных, возвращаемых инструментом.
    /// </summary>
    /// <param name="toolName"></param>
    /// <param name="result"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Форматирование сообщения ассистента на основе успешных вызовов инструментов. 
    /// Если ни один инструмент не завершился успешно, возвращается сообщение о том, что все вызовы завершились ошибкой.
    /// </summary>
    /// <param name="calls"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Форматирование информации о workspace для сообщения ассистента. 
    /// Если в workspace нет top-level директорий или файлов, возвращается соответствующее сообщение.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
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
    /// <summary>
    /// Форматирование списка файлов для сообщения ассистента. 
    /// Если список пуст, возвращается сообщение о том, что подходящих файлов не найдено.
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Формирование результата выполнения shell-команды для сообщения ассистента.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
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

    private static string FormatToolResultForModel(string toolName, AgentToolResult result)
    {
        if (!result.IsSuccess)
        {
            return $"{toolName} failed: {result.Error}";
        }

        return result.Data switch
        {
            WorkspaceInfo info => FormatWorkspaceInfo(info),
            IReadOnlyList<WorkspaceFile> files => FormatFiles(files),
            WorkspaceFileContent file => FormatFileContent(file),
            WorkspaceWriteResult write => $"Wrote {write.RelativePath} ({write.SizeBytes} bytes). Created: {write.WasCreated}. Overwritten: {write.WasOverwritten}.",
            ShellCommandResult shell => FormatShellResult(shell),
            _ => $"{toolName} completed."
        };
    }

    private sealed record ToolPlan(string ToolName, IReadOnlyDictionary<string, string?> Arguments);
}




