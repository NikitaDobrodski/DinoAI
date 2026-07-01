using DinoAI.Core.Permissions;
using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class WriteWorkspaceFileTool(
    IWorkspaceService workspace,
    IToolPermissionService permissions) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.write_file",
        "Записать UTF-8 текстовый файл внутри рабочей папки. Требует явного разрешения.",
        [
            new AgentToolParameter("path", "Относительный путь к файлу внутри рабочей папки.", true),
            new AgentToolParameter("content", "Текст для записи в файл.", true),
            new AgentToolParameter("overwrite", "Можно ли заменить существующий файл.", false, "false"),
            new AgentToolParameter("confirm", "Установи true, чтобы явно разрешить запись.", false, "false")
        ]);

    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default)
    {
        var path = context.Arguments.GetString("path", string.Empty);
        var approved = context.IsUserApproved || context.Arguments.GetBoolean("confirm", false);
        var permission = permissions.Evaluate(new ToolPermissionRequest(
            Definition.Name,
            ToolPermissionAction.WriteWorkspace,
            context.WorkspaceRoot,
            path,
            approved));

        if (!permission.IsAllowed)
        {
            return AgentToolResult.Failure($"Permission {permission.Decision}: {permission.Reason}");
        }

        try
        {
            var content = context.Arguments.GetString("content", string.Empty);
            var overwrite = context.Arguments.GetBoolean("overwrite", false);
            var result = await workspace.WriteFileAsync(context.WorkspaceRoot, path, content, overwrite, cancellationToken);
            return AgentToolResult.Success(result);
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return AgentToolResult.Failure(ex.Message);
        }
    }
}

