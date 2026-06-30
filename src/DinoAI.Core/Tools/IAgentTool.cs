namespace DinoAI.Core.Tools;

public interface IAgentTool
{
    AgentToolDefinition Definition { get; }

    Task<AgentToolResult> ExecuteAsync(AgentToolContext context, CancellationToken cancellationToken = default);
}
