namespace DinoAI.Core.Permissions;

public interface IToolPermissionService
{
    ToolPermissionResult Evaluate(ToolPermissionRequest request);
}
