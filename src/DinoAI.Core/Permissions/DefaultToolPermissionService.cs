namespace DinoAI.Core.Permissions;

public sealed class DefaultToolPermissionService : IToolPermissionService
{
    public ToolPermissionResult Evaluate(ToolPermissionRequest request)
    {
        return request.Action switch
        {
            ToolPermissionAction.ReadWorkspace => new ToolPermissionResult(
                ToolPermissionDecision.Allow,
                "Workspace read operations are allowed."),
            ToolPermissionAction.WriteWorkspace when request.IsUserApproved => new ToolPermissionResult(
                ToolPermissionDecision.Allow,
                "Workspace write operation was explicitly approved."),
            ToolPermissionAction.WriteWorkspace => new ToolPermissionResult(
                ToolPermissionDecision.Ask,
                "Workspace write operations require explicit approval."),
            ToolPermissionAction.RunShell when request.IsUserApproved => new ToolPermissionResult(
                ToolPermissionDecision.Allow,
                "Shell command was explicitly approved."),
            ToolPermissionAction.RunShell => new ToolPermissionResult(
                ToolPermissionDecision.Ask,
                "Shell commands require explicit approval."),
            _ => new ToolPermissionResult(
                ToolPermissionDecision.Deny,
                "Unsupported permission action.")
        };
    }
}
