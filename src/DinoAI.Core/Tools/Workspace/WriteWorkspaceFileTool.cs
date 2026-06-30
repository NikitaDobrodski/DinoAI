using DinoAI.Core.Permissions;
using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class WriteWorkspaceFileTool(
    IWorkspaceService workspace,
    IToolPermissionService permissions) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.write_file",
        "Write a UTF-8 text file inside the workspace root. Requires explicit approval.",
        [
            new AgentToolParameter("path", "Relative file path inside the workspace root.", true),
            new AgentToolParameter("content", "Text content to write.", true),
            new AgentToolParameter("overwrite", "Whether an existing file may be replaced.", false, "false"),
            new AgentToolParameter("confirm", "Set to true to explicitly approve this write operation.", false, "false")
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
