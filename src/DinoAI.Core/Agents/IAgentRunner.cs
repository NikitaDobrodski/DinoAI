namespace DinoAI.Core.Agents;

public interface IAgentRunner
{
    Task<AgentTurnResult> RunAsync(
        Guid sessionId,
        string workspaceRoot,
        string userMessage,
        CancellationToken cancellationToken = default);
}
