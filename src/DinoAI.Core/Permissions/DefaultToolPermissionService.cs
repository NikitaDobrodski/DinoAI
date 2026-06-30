using DinoAI.Core.Shell;

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
            ToolPermissionAction.RunShell when request.IsUserApproved || IsAllowlistedShellCommand(request.Target) => new ToolPermissionResult(
                ToolPermissionDecision.Allow,
                "Shell command is approved or allowlisted."),
            ToolPermissionAction.RunShell => new ToolPermissionResult(
                ToolPermissionDecision.Ask,
                "Shell commands require explicit approval unless they are allowlisted."),
            _ => new ToolPermissionResult(
                ToolPermissionDecision.Deny,
                "Unsupported permission action.")
        };
    }

    private static bool IsAllowlistedShellCommand(string? command)
    {
        return !string.IsNullOrWhiteSpace(command) && ShellCommandPolicy.IsAllowedWithoutApproval(command);
    }
}
