namespace DinoAI.Core.Tools;

public sealed class AgentToolRegistry(IEnumerable<IAgentTool> tools) : IAgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools = tools.ToDictionary(
        tool => tool.Definition.Name,
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AgentToolDefinition> List()
    {
        return _tools.Values
            .Select(tool => tool.Definition)
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<AgentToolResult> ExecuteAsync(
        string toolName,
        AgentToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return Task.FromResult(AgentToolResult.Failure($"Tool '{toolName}' was not found."));
        }

        return tool.ExecuteAsync(context, cancellationToken);
    }
}
