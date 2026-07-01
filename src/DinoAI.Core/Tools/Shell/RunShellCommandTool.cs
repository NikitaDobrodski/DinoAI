using DinoAI.Core.Permissions;
using DinoAI.Core.Shell;

namespace DinoAI.Core.Tools.Shell;

public sealed class RunShellCommandTool(
    IShellCommandRunner shell,
    IToolPermissionService permissions) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "shell.run",
        "Запустить разрешенную shell-команду в рабочей папке. Остальные команды требуют явного разрешения.",
        [
            new AgentToolParameter("command", "Команда для запуска в рабочей папке.", true),
            new AgentToolParameter("timeoutSeconds", "Максимальное время выполнения команды в секундах.", false, "60"),
            new AgentToolParameter("confirm", "Установи true, чтобы явно разрешить команду вне allowlist.", false, "false")
        ]);

    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default)
    {
        var command = context.Arguments.GetString("command", string.Empty);
        var approved = context.IsUserApproved || context.Arguments.GetBoolean("confirm", false) || ShellCommandPolicy.IsAllowedWithoutApproval(command);
        var permission = permissions.Evaluate(new ToolPermissionRequest(
            Definition.Name,
            ToolPermissionAction.RunShell,
            context.WorkspaceRoot,
            command,
            approved));

        if (!permission.IsAllowed)
        {
            return AgentToolResult.Failure($"Разрешение {permission.Decision}: {permission.Reason}");
        }

        try
        {
            var timeoutSeconds = context.Arguments.GetInt32("timeoutSeconds", 60);
            var result = await shell.RunAsync(context.WorkspaceRoot, command, timeoutSeconds, cancellationToken);
            return AgentToolResult.Success(result);
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or InvalidOperationException)
        {
            return AgentToolResult.Failure(ex.Message);
        }
    }
}

