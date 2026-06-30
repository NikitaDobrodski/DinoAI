namespace DinoAI.Core.Workspace;

public sealed record WorkspaceInfo(
    string RootPath,
    bool Exists,
    IReadOnlyList<string> TopLevelDirectories,
    IReadOnlyList<string> TopLevelFiles);
