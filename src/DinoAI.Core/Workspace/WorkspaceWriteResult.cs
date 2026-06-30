namespace DinoAI.Core.Workspace;

public sealed record WorkspaceWriteResult(
    string RelativePath,
    long SizeBytes,
    bool WasCreated,
    bool WasOverwritten);
