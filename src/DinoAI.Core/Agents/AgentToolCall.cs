using DinoAI.Core.Tools;

namespace DinoAI.Core.Agents;

public sealed record AgentToolCall(
    string ToolName,
    IReadOnlyDictionary<string, string?> Arguments,
    AgentToolResult Result);
