using DinoAI.Core.Agents;
using DinoAI.Core.Sessions;
using DinoAI.Core.Tools;
using DinoAI.Core.Tools.Workspace;
using DinoAI.Core.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAgentSessionStore, InMemoryAgentSessionStore>();
builder.Services.AddSingleton<IWorkspaceService, FileSystemWorkspaceService>();
builder.Services.AddSingleton<IAgentTool, DescribeWorkspaceTool>();
builder.Services.AddSingleton<IAgentTool, FindWorkspaceFilesTool>();
builder.Services.AddSingleton<IAgentTool, ReadWorkspaceFileTool>();
builder.Services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();
builder.Services.AddSingleton<IAgentRunner, LocalAgentRunner>();

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

app.MapPost("/sessions/{sessionId:guid}/turn", async (
    Guid sessionId,
    RunTurnRequest request,
    IAgentRunner agent,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await agent.RunAsync(
            sessionId,
            GetWorkspaceRoot(request.WorkspaceRoot),
            request.Content,
            cancellationToken);

        return Results.Ok(result);
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

app.MapGet("/tools", (IAgentToolRegistry tools) => Results.Ok(tools.List()));

app.MapPost("/tools/{toolName}/execute", async (
    string toolName,
    ExecuteToolRequest request,
    IAgentToolRegistry tools,
    CancellationToken cancellationToken) =>
{
    var context = new AgentToolContext(
        GetWorkspaceRoot(request.WorkspaceRoot),
        request.Arguments ?? new Dictionary<string, string?>());

    var result = await tools.ExecuteAsync(toolName, context, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();

static string GetWorkspaceRoot(string? root)
{
    return string.IsNullOrWhiteSpace(root) ? WorkspaceRootResolver.Resolve(Directory.GetCurrentDirectory()) : root;
}

public sealed record CreateSessionRequest(string? Title);

public sealed record AddMessageRequest(AgentMessageRole Role, string Content);

public sealed record RunTurnRequest(string? WorkspaceRoot, string Content);

public sealed record ExecuteToolRequest(string? WorkspaceRoot, Dictionary<string, string?>? Arguments);

