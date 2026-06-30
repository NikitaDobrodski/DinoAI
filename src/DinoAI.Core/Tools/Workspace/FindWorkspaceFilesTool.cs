using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class FindWorkspaceFilesTool(IWorkspaceService workspace) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.find_files",
        "Find files in the workspace by search pattern while ignoring build and tool folders.",
        [
            new AgentToolParameter("pattern", "File search pattern, for example *.cs or *.csproj.", false, "*"),
            new AgentToolParameter("maxResults", "Maximum number of files to return.", false, "200")
        ]);

    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = context.Arguments.GetString("pattern", "*");
            var maxResults = context.Arguments.GetInt32("maxResults", 200);
            var files = await workspace.FindFilesAsync(context.WorkspaceRoot, pattern, maxResults, cancellationToken);
            return AgentToolResult.Success(files);
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException)
        {
            return AgentToolResult.Failure(ex.Message);
        }
    }
}
