using DinoAI.Core.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAgentSessionStore, InMemoryAgentSessionStore>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/sessions"));

app.MapGet("/sessions", async (IAgentSessionStore sessions, CancellationToken cancellationToken) =>
{
    return Results.Ok(await sessions.ListAsync(cancellationToken));
});

app.MapPost("/sessions", async (CreateSessionRequest request, IAgentSessionStore sessions, CancellationToken cancellationToken) =>
{
    var session = await sessions.CreateAsync(request.Title, cancellationToken);
    return Results.Created($"/sessions/{session.Id}", session);
});

app.MapGet("/sessions/{sessionId:guid}", async (Guid sessionId, IAgentSessionStore sessions, CancellationToken cancellationToken) =>
{
    var session = await sessions.GetAsync(sessionId, cancellationToken);
    return session is null ? Results.NotFound() : Results.Ok(session);
});

app.MapPost("/sessions/{sessionId:guid}/messages", async (
    Guid sessionId,
    AddMessageRequest request,
    IAgentSessionStore sessions,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = await sessions.AddMessageAsync(sessionId, request.Role, request.Content, cancellationToken);
        return Results.Ok(session);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

public sealed record CreateSessionRequest(string? Title);

public sealed record AddMessageRequest(AgentMessageRole Role, string Content);
