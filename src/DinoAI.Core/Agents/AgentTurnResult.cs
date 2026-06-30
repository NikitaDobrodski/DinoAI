using DinoAI.Core.Sessions;
using DinoAI.Core.Tools;

namespace DinoAI.Core.Agents;

public sealed record AgentTurnResult(
    AgentSession Session,
    IReadOnlyList<AgentToolCall> ToolCalls);
