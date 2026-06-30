namespace DinoAI.Core.Workspace;

public interface IWorkspaceService
{
    Task<WorkspaceInfo> DescribeAsync(string rootPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceFile>> FindFilesAsync(
        string rootPath,
        string searchPattern = "*",
        int maxResults = 200,
        CancellationToken cancellationToken = default);

    Task<WorkspaceFileContent> ReadFileAsync(
        string rootPath,
        string relativePath,
        int maxBytes = 64 * 1024,
        CancellationToken cancellationToken = default);

    Task<WorkspaceWriteResult> WriteFileAsync(
        string rootPath,
        string relativePath,
        string content,
        bool overwrite = false,
        CancellationToken cancellationToken = default);
}
