using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class ReadWorkspaceFileTool(IWorkspaceService workspace) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.read_file",
        "Read a UTF-8 text file by relative path inside the workspace root.",
        [
            new AgentToolParameter("path", "Relative file path inside the workspace root.", true),
            new AgentToolParameter("maxBytes", "Maximum bytes to read before truncating.", false, "65536")
        ]);

    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = context.Arguments.GetString("path", string.Empty);
            var maxBytes = context.Arguments.GetInt32("maxBytes", 64 * 1024);
            var file = await workspace.ReadFileAsync(context.WorkspaceRoot, path, maxBytes, cancellationToken);
            return AgentToolResult.Success(file);
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException)
        {
            return AgentToolResult.Failure(ex.Message);
        }
    }
}
