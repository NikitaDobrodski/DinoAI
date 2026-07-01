using DinoAI.Core.Agents;
using DinoAI.Core.Models;
using DinoAI.Core.Permissions;
using DinoAI.Core.Sessions;
using DinoAI.Core.Shell;
using DinoAI.Core.Tools;
using DinoAI.Core.Tools.Shell;
using DinoAI.Core.Tools.Workspace;
using DinoAI.Core.Workspace;
using DinoAI.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var workspaceRoot = WorkspaceRootResolver.Resolve(Directory.GetCurrentDirectory());
var dinoStateRoot = Path.Combine(workspaceRoot, ".dinoai");
var sessionStorePath = Path.Combine(dinoStateRoot, "sessions.json");
var modelSettingsPath = Path.Combine(dinoStateRoot, "model-settings.json");

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChatModelSettingsStore>(_ => new FileChatModelSettingsStore(modelSettingsPath));
builder.Services.AddSingleton<IChatModelProvider, OpenAICompatibleChatModelProvider>();
builder.Services.AddSingleton<IAgentSessionStore>(_ => new FileAgentSessionStore(sessionStorePath));
builder.Services.AddSingleton<IWorkspaceService, FileSystemWorkspaceService>();
builder.Services.AddSingleton<IToolPermissionService, DefaultToolPermissionService>();
builder.Services.AddSingleton<IShellCommandRunner, ProcessShellCommandRunner>();
builder.Services.AddSingleton<IAgentTool, DescribeWorkspaceTool>();
builder.Services.AddSingleton<IAgentTool, FindWorkspaceFilesTool>();
builder.Services.AddSingleton<IAgentTool, ReadWorkspaceFileTool>();
builder.Services.AddSingleton<IAgentTool, WriteWorkspaceFileTool>();
builder.Services.AddSingleton<IAgentTool, RunShellCommandTool>();
builder.Services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();
builder.Services.AddSingleton<IAgentRunner, LocalAgentRunner>();
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





