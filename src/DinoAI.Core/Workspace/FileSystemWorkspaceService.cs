using System.Text;

namespace DinoAI.Core.Workspace;

public sealed class FileSystemWorkspaceService : IWorkspaceService
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dotnet",
        ".git",
        ".idea",
        ".nuget",
        ".templateengine",
        ".tmp",
        ".vs",
        ".vscode",
        "bin",
        "node_modules",
        "obj"
    };

    public Task<WorkspaceInfo> DescribeAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var root = NormalizeRoot(rootPath);
        if (!Directory.Exists(root))
        {
            return Task.FromResult(new WorkspaceInfo(root, false, [], []));
        }

        var directories = Directory.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !IgnoredDirectoryNames.Contains(name!))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var files = Directory.EnumerateFiles(root)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        return Task.FromResult(new WorkspaceInfo(root, true, directories, files));
    }

    public Task<IReadOnlyList<WorkspaceFile>> FindFilesAsync(
        string rootPath,
        string searchPattern = "*",
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        var root = NormalizeExistingRoot(rootPath);
        var safePattern = string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern.Trim();
        var safeMaxResults = Math.Clamp(maxResults, 1, 1000);

        var files = Directory.EnumerateFiles(root, safePattern, SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path, root))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(safeMaxResults)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new WorkspaceFile(ToRelativePath(root, path), info.Length, info.LastWriteTimeUtc);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<WorkspaceFile>>(files);
    }

    public async Task<WorkspaceFileContent> ReadFileAsync(
        string rootPath,
        string relativePath,
        int maxBytes = 64 * 1024,
        CancellationToken cancellationToken = default)
    {
        var root = NormalizeExistingRoot(rootPath);
        var filePath = ResolveInsideRoot(root, relativePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Workspace file was not found.", relativePath);
        }

        var safeMaxBytes = Math.Clamp(maxBytes, 1, 1024 * 1024);
        var info = new FileInfo(filePath);
        var byteCount = (int)Math.Min(info.Length, safeMaxBytes);
        var buffer = new byte[byteCount];

        await using var stream = File.OpenRead(filePath);
        var read = await stream.ReadAsync(buffer.AsMemory(0, byteCount), cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);

        return new WorkspaceFileContent(ToRelativePath(root, filePath), content, info.Length, info.Length > safeMaxBytes);
    }

    private static string NormalizeExistingRoot(string rootPath)
    {
        var root = NormalizeRoot(rootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace root '{root}' was not found.");
        }

        return root;
    }

    private static string NormalizeRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Workspace root cannot be empty.", nameof(rootPath));
        }

        return Path.GetFullPath(rootPath.Trim());
    }

    private static string ResolveInsideRoot(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(relativePath));
        }

        var combinedPath = Path.GetFullPath(Path.Combine(root, relativePath.Trim()));
        if (!IsInsideRoot(root, combinedPath))
        {
            throw new UnauthorizedAccessException("File path escapes the workspace root.");
        }

        return combinedPath;
    }

    private static bool IsIgnored(string path, string root)
    {
        var relative = ToRelativePath(root, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/');
        return parts.Any(part => IgnoredDirectoryNames.Contains(part));
    }

    private static bool IsInsideRoot(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
