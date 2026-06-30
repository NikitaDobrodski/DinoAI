namespace DinoAI.Core.Workspace;

public sealed record WorkspaceFileContent(
    string RelativePath,
    string Content,
    long SizeBytes,
    bool WasTruncated);
