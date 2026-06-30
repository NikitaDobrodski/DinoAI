namespace DinoAI.Core.Workspace;

public sealed record WorkspaceFile(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModifiedAt);
