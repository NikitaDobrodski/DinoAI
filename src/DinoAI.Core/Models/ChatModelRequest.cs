namespace DinoAI.Core.Models;

public sealed record ChatModelRequest(
    IReadOnlyList<ChatModelMessage> Messages,
    string? Model = null,
    double? Temperature = null);
