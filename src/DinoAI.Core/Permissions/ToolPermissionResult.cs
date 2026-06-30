namespace DinoAI.Core.Permissions;

public sealed record ToolPermissionResult(
    ToolPermissionDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == ToolPermissionDecision.Allow;
}
