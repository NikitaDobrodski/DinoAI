namespace DinoAI.Core.Workspace;

public static class WorkspaceRootResolver
{
    public static string Resolve(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            throw new ArgumentException("Start path cannot be empty.", nameof(startPath));
        }

        var current = Directory.Exists(startPath)
            ? new DirectoryInfo(Path.GetFullPath(startPath))
            : new FileInfo(Path.GetFullPath(startPath)).Directory;

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                Directory.EnumerateFiles(current.FullName, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                Directory.EnumerateFiles(current.FullName, "*.slnx", SearchOption.TopDirectoryOnly).Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startPath);
    }
}
