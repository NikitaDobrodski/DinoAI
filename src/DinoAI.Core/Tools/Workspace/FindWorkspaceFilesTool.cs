using DinoAI.Core.Workspace;

namespace DinoAI.Core.Tools.Workspace;

public sealed class FindWorkspaceFilesTool(IWorkspaceService workspace) : IAgentTool
{
    public AgentToolDefinition Definition { get; } = new(
        "workspace.find_files",
        "Найти файлы в рабочей папке по шаблону, пропуская build- и tool-каталоги.",
        [
            new AgentToolParameter("pattern", "Шаблон поиска файлов, например *.cs или *.csproj.", false, "*"),
            new AgentToolParameter("maxResults", "Максимальное число файлов в результате.", false, "200")
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

