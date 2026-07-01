using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class ReadWorkspaceFileTool(IWorkspaceService workspace) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.read_file",
        "Прочитать UTF-8 текстовый файл по относительному пути внутри рабочей папки.",
        [
            new AgentToolParameter("path", "Относительный путь к файлу внутри рабочей папки.", true),
            new AgentToolParameter("maxBytes", "Максимум байт для чтения до обрезки результата.", false, "65536")
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

