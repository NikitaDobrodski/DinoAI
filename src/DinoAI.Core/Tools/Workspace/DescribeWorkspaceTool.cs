using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class DescribeWorkspaceTool(IWorkspaceService workspace) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.describe",
        "Describe the workspace root with top-level directories and files.",
        []);

    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return AgentToolResult.Success(await workspace.DescribeAsync(context.WorkspaceRoot, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException)
        {
            return AgentToolResult.Failure(ex.Message);
        }
    }
}
