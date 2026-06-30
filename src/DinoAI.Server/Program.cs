using DinoAI.Core.Sessions;
using DinoAI.Core.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAgentSessionStore, InMemoryAgentSessionStore>();
builder.Services.AddSingleton<IWorkspaceService, FileSystemWorkspaceService>();

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

app.MapGet("/workspace", async (
    string? root,
    IWorkspaceService workspace,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await workspace.DescribeAsync(GetWorkspaceRoot(root), cancellationToken));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/workspace/files", async (
    string? root,
    string? pattern,
    int? maxResults,
    IWorkspaceService workspace,
    CancellationToken cancellationToken) =>
{
    try
    {
        var files = await workspace.FindFilesAsync(
            GetWorkspaceRoot(root),
            pattern ?? "*",
            maxResults ?? 200,
            cancellationToken);

        return Results.Ok(files);
    }
    catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/workspace/file", async (
    string? root,
    string path,
    int? maxBytes,
    IWorkspaceService workspace,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await workspace.ReadFileAsync(GetWorkspaceRoot(root), path, maxBytes ?? 64 * 1024, cancellationToken));
    }
    catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static string GetWorkspaceRoot(string? root)
{
    return string.IsNullOrWhiteSpace(root) ? Directory.GetCurrentDirectory() : root;
}

public sealed record CreateSessionRequest(string? Title);

public sealed record AddMessageRequest(AgentMessageRole Role, string Content);
