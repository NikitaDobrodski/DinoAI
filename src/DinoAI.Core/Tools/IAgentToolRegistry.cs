namespace DinoAI.Core.Tools;

public interface IAgentToolRegistry
{
    IReadOnlyList<AgentToolDefinition> List();

    Task<AgentToolResult> ExecuteAsync(
        string toolName,
        AgentToolContext context,
        CancellationToken cancellationToken = default);
}
