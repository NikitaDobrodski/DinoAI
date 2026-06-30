using DinoAI.Core.Sessions;
using DinoAI.Core.Workspace;

var sessions = new InMemoryAgentSessionStore();
var workspace = new FileSystemWorkspaceService();

if (args is ["new", .. var titleParts])
{
    var title = titleParts.Length == 0 ? null : string.Join(' ', titleParts);
    var session = await sessions.CreateAsync(title);
    Console.WriteLine($"Created session {session.Id}: {session.Title}");
    return;
}

if (args is ["demo"])
{
    var session = await sessions.CreateAsync("CLI demo session");
    session = await sessions.AddMessageAsync(session.Id, AgentMessageRole.User, "Hello DinoAI");
    session = await sessions.AddMessageAsync(session.Id, AgentMessageRole.Assistant, "DinoAI core session storage is alive.");

    Console.WriteLine($"Session: {session.Title} ({session.Id})");
    foreach (var message in session.Messages)
    {
        Console.WriteLine($"[{message.Role}] {message.Content}");
    }

    return;
}

if (args is ["workspace", .. var rootParts])
{
    var workspaceRoot = GetRoot(rootParts);
    var info = await workspace.DescribeAsync(workspaceRoot);
    Console.WriteLine($"Workspace: {info.RootPath}");
    Console.WriteLine($"Exists: {info.Exists}");
    Console.WriteLine("Directories:");
    foreach (var directory in info.TopLevelDirectories)
    {
        Console.WriteLine($"  {directory}/");
    }

    Console.WriteLine("Files:");
    foreach (var file in info.TopLevelFiles)
    {
        Console.WriteLine($"  {file}");
    }

    return;
}

if (args is ["files", .. var fileArgs])
{
    var filesRoot = fileArgs.Length >= 1 ? fileArgs[0] : Directory.GetCurrentDirectory();
    var pattern = fileArgs.Length >= 2 ? fileArgs[1] : "*";
    var files = await workspace.FindFilesAsync(filesRoot, pattern, 200);

    foreach (var file in files)
    {
        Console.WriteLine($"{file.RelativePath} ({file.SizeBytes} bytes)");
    }

    return;
}

if (args is ["read", var readRoot, var path])
{
    var file = await workspace.ReadFileAsync(readRoot, path);
    Console.Write(file.Content);
    if (file.WasTruncated)
    {
        Console.WriteLine();
        Console.WriteLine($"--- truncated at {file.Content.Length} characters from {file.SizeBytes} bytes ---");
    }

    return;
}

Console.WriteLine("DinoAI CLI");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  dino new [title]");
Console.WriteLine("  dino demo");
Console.WriteLine("  dino workspace [root]");
Console.WriteLine("  dino files [root] [pattern]");
Console.WriteLine("  dino read <root> <relative-path>");

static string GetRoot(string[] rootParts)
{
    return rootParts.Length == 0 ? Directory.GetCurrentDirectory() : string.Join(' ', rootParts);
}

