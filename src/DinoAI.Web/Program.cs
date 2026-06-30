using DinoAI.Core.Sessions;
using DinoAI.Core.Tools;
using DinoAI.Core.Tools.Workspace;
using DinoAI.Core.Workspace;
using DinoAI.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAgentSessionStore, InMemoryAgentSessionStore>();
builder.Services.AddSingleton<IWorkspaceService, FileSystemWorkspaceService>();
builder.Services.AddSingleton<IAgentTool, DescribeWorkspaceTool>();
builder.Services.AddSingleton<IAgentTool, FindWorkspaceFilesTool>();
builder.Services.AddSingleton<IAgentTool, ReadWorkspaceFileTool>();
builder.Services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
