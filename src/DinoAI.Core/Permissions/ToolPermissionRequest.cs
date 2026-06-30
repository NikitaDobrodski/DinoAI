namespace DinoAI.Core.Permissions;

public sealed record ToolPermissionRequest(
    string ToolName,
    ToolPermissionAction Action,
    string WorkspaceRoot,
    string? Target,
    bool IsUserApproved);
