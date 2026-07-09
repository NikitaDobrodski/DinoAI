using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using DinoAI.Core.Agents;
using DinoAI.Core.Models;
using DinoAI.Core.Permissions;
using DinoAI.Core.Sessions;
using DinoAI.Core.Shell;
using DinoAI.Core.Tools;
using DinoAI.Core.Tools.Shell;
using DinoAI.Core.Tools.Workspace;
using DinoAI.Core.Workspace;

var workspaceRoot = ResolveInitialWorkspaceRoot(args);
EnableVirtualTerminalRendering();
var dinoStateRoot = Path.Combine(workspaceRoot, ".dinoai");
var sessionStorePath = Path.Combine(dinoStateRoot, "sessions.json");
var modelSettingsPath = Path.Combine(dinoStateRoot, "model-settings.json");
var sessions = new FileAgentSessionStore(sessionStorePath);
var workspace = new FileSystemWorkspaceService();
var permissionService = new DefaultToolPermissionService();
var shellRunner = new ProcessShellCommandRunner();
var modelSettings = new FileChatModelSettingsStore(modelSettingsPath);
var modelProvider = new OpenAICompatibleChatModelProvider(new HttpClient(), modelSettings);
var tools = new AgentToolRegistry(
[
    new DescribeWorkspaceTool(workspace),
    new FindWorkspaceFilesTool(workspace),
    new ReadWorkspaceFileTool(workspace),
    new WriteWorkspaceFileTool(workspace, permissionService),
    new RunShellCommandTool(shellRunner, permissionService)
]);
var agent = new LocalAgentRunner(sessions, tools, modelProvider);

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};
int? animatedDinoTop = null;
var animatedDinoHeight = 0;
var animatedDinoStride = false;
var nextAnimatedDinoFrame = DateTimeOffset.UtcNow;

if (args.Length == 0 || args is ["chat", ..] or ["start", ..])
{
    var chatRoot = args.Length > 1 ? WorkspaceRootResolver.Resolve(string.Join(' ', args[1..])) : workspaceRoot;
    await RunInteractiveAsync(chatRoot);
    return;
}

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

if (args is ["ask", var askRoot, .. var messageParts])
{
    if (messageParts.Length == 0)
    {
        Console.WriteLine("Usage: dino ask <root> <message>");
        return;
    }

    var session = await sessions.CreateAsync("CLI ask session");
    var result = await agent.RunAsync(session.Id, askRoot, string.Join(' ', messageParts));
    foreach (var message in result.Session.Messages)
    {
        Console.WriteLine($"[{message.Role}] {message.Content}");
    }

    return;
}

if (args is ["workspace", .. var rootParts])
{
    var requestedWorkspaceRoot = GetRoot(rootParts);
    var info = await workspace.DescribeAsync(requestedWorkspaceRoot);
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

if (args is ["tools"])
{
    foreach (var tool in tools.List())
    {
        Console.WriteLine($"{tool.Name} - {tool.Description}");
        foreach (var parameter in tool.Parameters)
        {
            var required = parameter.IsRequired ? "required" : $"default={parameter.DefaultValue}";
            Console.WriteLine($"  {parameter.Name}: {parameter.Description} ({required})");
        }
    }

    return;
}

if (args is ["model", "groq", .. var groqArgs])
{
    var arguments = ParseArguments(groqArgs);
    var apiKey = arguments.GetValueOrDefault("apiKey")
        ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
        ?? Environment.GetEnvironmentVariable("DINOAI_OPENAI_API_KEY");
    var model = arguments.GetValueOrDefault("model") ?? OpenAICompatibleChatModelOptions.GroqDefaultModel;

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.WriteLine("Usage: dino model groq apiKey=<key> [model=qwen/qwen3-32b]");
        Console.WriteLine("Or set GROQ_API_KEY before running this command.");
        return;
    }

    var saved = await modelSettings.SaveAsync(new OpenAICompatibleChatModelOptions(
        OpenAICompatibleChatModelOptions.GroqBaseUrl,
        apiKey,
        model));

    Console.WriteLine("Saved Groq model settings.");
    Console.WriteLine($"Base URL: {saved.BaseUrl}");
    Console.WriteLine($"Model: {saved.Model}");
    return;
}

if (args is ["model", "status"])
{
    var status = modelProvider.GetStatus();
    Console.WriteLine($"Provider: {status.ProviderName}");
    Console.WriteLine($"Configured: {status.IsConfigured}");
    Console.WriteLine($"Model: {status.Model}");
    if (!string.IsNullOrWhiteSpace(status.Reason))
    {
        Console.WriteLine($"Reason: {status.Reason}");
    }

    return;
}

if (args is ["tool", var toolName, var toolRoot, .. var toolArgs])
{
    var arguments = ParseArguments(toolArgs);
    var result = await tools.ExecuteAsync(toolName, new AgentToolContext(toolRoot, arguments, arguments.GetBoolean("confirm", false)));
    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    return;
}

if (args[0].StartsWith("-", StringComparison.Ordinal) is false)
{
    await RunOneShotAsync(workspaceRoot, string.Join(' ', args));
    return;
}

Console.WriteLine("DinoAI CLI");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  dino");
Console.WriteLine("  dino chat [root]");
Console.WriteLine("  dino <message>");
Console.WriteLine("  dino new [title]");
Console.WriteLine("  dino demo");
Console.WriteLine("  dino ask <root> <message>");
Console.WriteLine("  dino workspace [root]");
Console.WriteLine("  dino files [root] [pattern]");
Console.WriteLine("  dino read <root> <relative-path>");
Console.WriteLine("  dino tools");
Console.WriteLine("  dino model groq [apiKey=<key>] [model=qwen/qwen3-32b]");
Console.WriteLine("  dino model status");
Console.WriteLine("  dino tool <name> <root> [key=value ...]");

static string GetRoot(string[] rootParts)
{
    return rootParts.Length == 0 ? Directory.GetCurrentDirectory() : string.Join(' ', rootParts);
}

static IReadOnlyDictionary<string, string?> ParseArguments(string[] args)
{
    var arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var arg in args)
    {
        var separator = arg.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        arguments[arg[..separator]] = arg[(separator + 1)..];
    }

    return arguments;
}

static string ResolveInitialWorkspaceRoot(string[] args)
{
    var root = args switch
    {
        ["ask", var askRoot, ..] => askRoot,
        ["tool", _, var toolRoot, ..] => toolRoot,
        ["read", var readRoot, ..] => readRoot,
        ["files", var filesRoot, ..] => filesRoot,
        ["workspace", .. var rootParts] when rootParts.Length > 0 => string.Join(' ', rootParts),
        _ => Directory.GetCurrentDirectory()
    };

    return WorkspaceRootResolver.Resolve(root);
}

static void EnableVirtualTerminalRendering()
{
    if (!OperatingSystem.IsWindows() || Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        var handle = ConsoleNative.GetStdHandle(ConsoleNative.StdOutputHandle);
        if (handle == IntPtr.Zero || handle == ConsoleNative.InvalidHandleValue)
        {
            return;
        }

        if (!ConsoleNative.GetConsoleMode(handle, out var mode))
        {
            return;
        }

        _ = ConsoleNative.SetConsoleMode(handle, mode | ConsoleNative.EnableVirtualTerminalProcessing);
    }
    catch
    {
    }
}

async Task RunInteractiveAsync(string chatRoot)
{
    PrintBanner();
    PrintStatus(chatRoot);

    var session = await sessions.CreateAsync("DinoAI terminal session");
    while (true)
    {
        var input = ReadAnimatedPrompt();
        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)
            || input.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("bye");
            return;
        }

        if (await TryHandleLocalInteractiveCommandAsync(input, chatRoot))
        {
            continue;
        }

        var previousCount = session.Messages.Count;
        var result = await agent.RunAsync(session.Id, chatRoot, input);
        session = result.Session;
        foreach (var message in session.Messages.Skip(previousCount))
        {
            if (message.Role is AgentMessageRole.User)
            {
                continue;
            }

            PrintMessage(message);
        }
    }
}

async Task<bool> TryHandleLocalInteractiveCommandAsync(string input, string chatRoot)
{
    var trimmed = input.Trim();
    var lower = trimmed.ToLowerInvariant();

    if (lower is "/help" or "/commands" or "/")
    {
        PrintCommandPalette();
        return true;
    }

    if (lower is "/models" or "/model")
    {
        PrintModelStatus();
        return true;
    }

    if (lower is "/clear" or "clear")
    {
        Console.Clear();
        PrintBanner();
        PrintStatus(chatRoot);
        return true;
    }

    if (lower is "/workspace")
    {
        var info = await workspace.DescribeAsync(chatRoot);
        PrintWorkspaceInfo(info);
        return true;
    }

    if (lower.StartsWith("/files", StringComparison.Ordinal))
    {
        var pattern = trimmed.Length > "/files".Length ? trimmed["/files".Length..].Trim() : "*";
        var files = await workspace.FindFilesAsync(chatRoot, string.IsNullOrWhiteSpace(pattern) ? "*" : pattern, 80);
        PrintFileList(files);
        return true;
    }

    if (lower.StartsWith("/read ", StringComparison.Ordinal))
    {
        var path = trimmed["/read ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            WriteMuted("usage: /read <relative-path>");
            return true;
        }

        try
        {
            var file = await workspace.ReadFileAsync(chatRoot, path, 16 * 1024);
            WriteSection(file.RelativePath);
            Console.WriteLine(file.Content);
            if (file.WasTruncated)
            {
                WriteMuted($"[truncated at {file.Content.Length} chars from {file.SizeBytes} bytes]");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or UnauthorizedAccessException)
        {
            WriteError(ex.Message);
        }

        return true;
    }

    return false;
}

string? ReadAnimatedPrompt()
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("DinoAI> ");
        Console.ResetColor();
        return Console.ReadLine();
    }

    PrintInputBoxTop();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("│ ");
    Console.ResetColor();

    var buffer = new StringBuilder();
    while (true)
    {
        MaybeAnimateDinoIdle();

        if (!Console.KeyAvailable)
        {
            Thread.Sleep(25);
            continue;
        }

        var key = Console.ReadKey(intercept: true);
        if (key.Key is ConsoleKey.Enter)
        {
            Console.WriteLine();
            PrintInputBoxBottom();
            return buffer.ToString();
        }

        if (key.Key is ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
                Console.Write("\b \b");
            }

            continue;
        }

        if (key.Key is ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            Console.WriteLine("^C");
            PrintInputBoxBottom();
            return "/exit";
        }

        if (key.Key is ConsoleKey.P && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            Console.WriteLine();
            PrintInputBoxBottom();
            return "/help";
        }

        if (!char.IsControl(key.KeyChar))
        {
            buffer.Append(key.KeyChar);
            Console.Write(key.KeyChar);
        }
    }
}

static void PrintInputBoxTop()
{
    var width = GetInputBoxWidth();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("┌");
    Console.Write(" DinoAI input ");
    Console.Write(new string('─', Math.Max(0, width - 31)));
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write(" ctrl+p commands ");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("┐");
    Console.ResetColor();
}

static void PrintInputBoxBottom()
{
    var width = GetInputBoxWidth();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("└");
    Console.Write(new string('─', Math.Max(0, width - 2)));
    Console.WriteLine("┘");
    Console.ResetColor();
}

static int GetInputBoxWidth()
{
    try
    {
        return Math.Clamp(Console.WindowWidth - 4, 48, 110);
    }
    catch (IOException)
    {
        return 80;
    }
}

void MaybeAnimateDinoIdle()
{
    if (!CanAnimateIdleDino() || DateTimeOffset.UtcNow < nextAnimatedDinoFrame)
    {
        return;
    }

    animatedDinoStride = !animatedDinoStride;
    RedrawDinoAtBanner(animatedDinoStride);
    nextAnimatedDinoFrame = DateTimeOffset.UtcNow.AddMilliseconds(360);
}

bool CanAnimateIdleDino()
{
    try
    {
        return animatedDinoTop is not null
            && animatedDinoHeight > 0
            && !Console.IsOutputRedirected
            && Console.CursorTop > animatedDinoTop + animatedDinoHeight;
    }
    catch (IOException)
    {
        return false;
    }
}

void RedrawDinoAtBanner(bool stride)
{
    if (animatedDinoTop is null)
    {
        return;
    }

    try
    {
        var cursorLeft = Console.CursorLeft;
        var cursorTop = Console.CursorTop;
        ClearDinoFrame(animatedDinoTop.Value, animatedDinoHeight, Math.Min(Console.WindowWidth - 1, GetReferenceDinoWidth() + 2));
        Console.SetCursorPosition(0, animatedDinoTop.Value);
        PrintReferenceDino(stride: stride);
        Console.SetCursorPosition(cursorLeft, cursorTop);
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}

async Task RunOneShotAsync(string askRoot, string message)
{
    PrintBanner(compact: true);
    var session = await sessions.CreateAsync("DinoAI one-shot session");
    var result = await agent.RunAsync(session.Id, askRoot, message);
    foreach (var response in result.Session.Messages.Where(message => message.Role is not AgentMessageRole.User))
    {
        PrintMessage(response);
    }
}

void PrintStatus(string chatRoot)
{
    var status = modelProvider.GetStatus();
    PrintRightStatusPanel(chatRoot, status);
    Console.ForegroundColor = status.IsConfigured ? ConsoleColor.Green : ConsoleColor.Yellow;
    Console.WriteLine(status.IsConfigured
        ? $"model: {status.Model}"
        : $"model: not configured ({status.Reason})");
    Console.ResetColor();
    Console.WriteLine($"workspace: {chatRoot}");
    Console.WriteLine("commands: /workspace, /files *.cs, /read README.md, /build, /status, /exit");
    Console.WriteLine();
}

static void PrintRightStatusPanel(string chatRoot, ChatModelProviderStatus status)
{
    if (Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        if (Console.WindowWidth < 104)
        {
            return;
        }

        var left = Console.WindowWidth - 38;
        var top = 2;
        var cursorLeft = Console.CursorLeft;
        var cursorTop = Console.CursorTop;
        var lines = new[]
        {
            "DinoAI",
            $"model  {TrimForPanel(status.Model ?? "unknown", 26)}",
            $"state  {(status.IsConfigured ? "connected" : "not configured")}",
            $"root   {TrimForPanel(chatRoot, 26)}",
            "keys   ctrl+p commands",
            "local  /files /read /workspace"
        };

        DrawPanel(left, top, 34, "status", lines);
        Console.SetCursorPosition(cursorLeft, cursorTop);
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}

static void DrawPanel(int left, int top, int width, string title, IReadOnlyList<string> lines)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.SetCursorPosition(left, top);
    Console.Write("┌" + title.PadRight(width - 2, '─') + "┐");

    for (var index = 0; index < lines.Count; index++)
    {
        Console.SetCursorPosition(left, top + index + 1);
        Console.Write("│");
        Console.ForegroundColor = index is 0 ? ConsoleColor.Green : ConsoleColor.Gray;
        Console.Write((" " + lines[index]).PadRight(width - 2));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("│");
    }

    Console.SetCursorPosition(left, top + lines.Count + 1);
    Console.Write("└" + new string('─', width - 2) + "┘");
    Console.ResetColor();
}

static string TrimForPanel(string value, int maxLength)
{
    if (value.Length <= maxLength)
    {
        return value;
    }

    return "..." + value[^Math.Max(0, maxLength - 3)..];
}

void PrintCommandPalette()
{
    var commands = new (string Command, string Description)[]
    {
        ("/help", "show commands"),
        ("/models", "show model/provider status"),
        ("/workspace", "show current workspace"),
        ("/files [pattern]", "list files, example: /files *.cs"),
        ("/read <path>", "read a workspace file"),
        ("/status", "git status"),
        ("/diff", "git diff summary"),
        ("/build", "build current .NET workspace"),
        ("/clear", "clear screen"),
        ("/exit", "exit DinoAI")
    };

    Console.WriteLine();
    WriteSection("commands");
    foreach (var (command, description) in commands)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {command,-18}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(description);
    }

    Console.ResetColor();
    WriteMuted("tip: local commands do not spend Groq tokens");
    Console.WriteLine();
}

void PrintModelStatus()
{
    var status = modelProvider.GetStatus();
    WriteSection("model");
    Console.Write("  provider   ");
    WriteValue(status.ProviderName);
    Console.Write("  configured ");
    WriteValue(status.IsConfigured ? "yes" : "no", status.IsConfigured ? ConsoleColor.Green : ConsoleColor.Yellow);
    Console.Write("  model      ");
    WriteValue(status.Model ?? "unknown");
    if (!string.IsNullOrWhiteSpace(status.Reason))
    {
        Console.Write("  reason     ");
        WriteMuted(status.Reason);
    }

    Console.WriteLine();
}

static void PrintWorkspaceInfo(WorkspaceInfo info)
{
    WriteSection("workspace");
    Console.Write("  root       ");
    WriteValue(info.RootPath);
    Console.Write("  exists     ");
    WriteValue(info.Exists ? "yes" : "no", info.Exists ? ConsoleColor.Green : ConsoleColor.Yellow);

    if (info.TopLevelDirectories.Count > 0)
    {
        Console.WriteLine("  folders");
        foreach (var directory in info.TopLevelDirectories.Take(16))
        {
            Console.WriteLine($"    {directory}/");
        }
    }

    if (info.TopLevelFiles.Count > 0)
    {
        Console.WriteLine("  files");
        foreach (var file in info.TopLevelFiles.Take(16))
        {
            Console.WriteLine($"    {file}");
        }
    }

    Console.WriteLine();
}

static void PrintFileList(IReadOnlyList<WorkspaceFile> files)
{
    WriteSection($"files ({files.Count})");
    foreach (var file in files.Take(24))
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {file.RelativePath}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {file.SizeBytes} bytes");
    }

    Console.ResetColor();
    if (files.Count > 24)
    {
        WriteMuted($"  ...and {files.Count - 24} more. Narrow with /files <pattern>");
    }

    Console.WriteLine();
}

static void WriteSection(string title)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(title);
    Console.ResetColor();
}

static void WriteValue(string value, ConsoleColor color = ConsoleColor.White)
{
    Console.ForegroundColor = color;
    Console.WriteLine(value);
    Console.ResetColor();
}

static void WriteMuted(string value)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(value);
    Console.ResetColor();
}

static void WriteError(string value)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(value);
    Console.ResetColor();
}

static void PrintMessage(AgentMessage message)
{
    var color = message.Role switch
    {
        AgentMessageRole.Assistant => ConsoleColor.White,
        AgentMessageRole.Tool => ConsoleColor.DarkGray,
        AgentMessageRole.System => ConsoleColor.DarkYellow,
        _ => ConsoleColor.Gray
    };

    Console.ForegroundColor = color;
    Console.WriteLine(message.Role is AgentMessageRole.Tool ? $"tool: {message.Content}" : message.Content);
    Console.ResetColor();
    Console.WriteLine();
}

void PrintBanner(bool compact = false)
{
    if (!CanUseTerminalCursor())
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("DinoAI");
        Console.ResetColor();
        if (!compact)
        {
            Console.WriteLine("local coding agent - Groq/OpenAI-compatible tools enabled");
            Console.WriteLine();
        }

        return;
    }

    if (compact)
    {
        animatedDinoTop = null;
        animatedDinoHeight = 0;
        PrintReferenceDino();
    }
    else
    {
        animatedDinoTop = Console.CursorTop;
        animatedDinoHeight = (GetReferenceDinoRows(stride: false).Length + 1) / 2;
        AnimateReferenceDino();
        nextAnimatedDinoFrame = DateTimeOffset.UtcNow.AddMilliseconds(360);
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("DinoAI");
    Console.ResetColor();

    if (!compact)
    {
        Console.WriteLine("local coding agent - Groq/OpenAI-compatible tools enabled");
        Console.WriteLine();
    }
}

static bool CanUseTerminalCursor()
{
    if (Console.IsOutputRedirected)
    {
        return false;
    }

    try
    {
        _ = Console.CursorTop;
        _ = Console.WindowWidth;
        return true;
    }
    catch (IOException)
    {
        return false;
    }
}

static void AnimateReferenceDino()
{
    if (!CanAnimateDino())
    {
        PrintReferenceDino();
        return;
    }

    var startTop = Console.CursorTop;
    var frameHeight = (GetReferenceDinoRows(stride: false).Length + 1) / 2;
    var clearWidth = Math.Min(Console.WindowWidth - 1, GetReferenceDinoWidth() + 18);
    var cursorWasVisible = Console.CursorVisible;

    try
    {
        Console.CursorVisible = false;
        var indents = new[] { 16, 11, 7, 3, 0 };
        for (var frame = 0; frame < indents.Length; frame++)
        {
            ClearDinoFrame(startTop, frameHeight, clearWidth);
            Console.SetCursorPosition(0, startTop);
            PrintReferenceDino(stride: frame % 2 is 1, indent: indents[frame]);
            Thread.Sleep(65);
        }

        ClearDinoFrame(startTop, frameHeight, clearWidth);
        Console.SetCursorPosition(0, startTop);
        PrintReferenceDino();
    }
    catch (IOException)
    {
        Console.SetCursorPosition(0, startTop);
        PrintReferenceDino();
    }
    finally
    {
        Console.CursorVisible = cursorWasVisible;
    }
}

static bool CanAnimateDino()
{
    try
    {
        return !Console.IsOutputRedirected
            && Console.WindowWidth > GetReferenceDinoWidth() + 2
            && Console.BufferHeight > Console.CursorTop + 14;
    }
    catch (IOException)
    {
        return false;
    }
}

static void ClearDinoFrame(int startTop, int height, int width)
{
    Console.Write("\x1b[0m");
    for (var line = 0; line < height; line++)
    {
        Console.SetCursorPosition(0, startTop + line);
        Console.Write(new string(' ', width));
    }
}

static void PrintReferenceDino(bool stride = false, int indent = 0)
{
    var rows = GetReferenceDinoRows(stride);

    var width = rows.Max(row => row.Length);
    for (var rowIndex = 0; rowIndex < rows.Length; rowIndex += 2)
    {
        if (indent > 0)
        {
            Console.Write(new string(' ', indent));
        }

        var top = rows[rowIndex];
        var bottom = rowIndex + 1 < rows.Length ? rows[rowIndex + 1] : string.Empty;
        for (var column = 0; column < width; column++)
        {
            var topPixel = column < top.Length ? top[column] : '.';
            var bottomPixel = column < bottom.Length ? bottom[column] : '.';
            PrintHalfBlockPixel(topPixel, bottomPixel);
        }

        Console.Write("\x1b[0m");
        Console.WriteLine();
    }
}

static string[] GetReferenceDinoRows(bool stride)
{
    return stride
        ? [
            ".XX..RDDR",
            "..DDRKXRRRR",
            "DRRSRXRRRRRR",
            "RRRSKKDSKDRRR",
            "KDDDCCCCCSKKR",
            ".CCCCCCCCCSRRR...................................DRDDK",
            "........SCCCKRSRRR...........................KRRRK.....K",
            ".........SCCCSKKDDRRRKDRR...............RSRRRKK",
            "..........CCCCCCCCKKKKDRKKKDKRR....RRRRRKKKKS",
            "..........XCCSSCCCCCCCCRKRRRDKKKKKKKSCCCC",
            "...........CCCRRCCCCCCCSKRRRDSCCCCCCCC",
            "............S..CCCCCCCCRDRRRSSCCC",
            "..................CCCCSKRKRR",
            ".................XXX.....DRRR",
            "................DDX.......RRR",
            ".................DDD......DDR",
            "..................DDD.......R",
            "..............SSD..........RR"
        ]
        : [
            ".XX..RDDR",
            "..DDRKXRRRR",
            "DRRSRXRRRRRR",
            "RRRSKKDSKDRRR",
            "KDDDCCCCCSKKR",
            ".CCCCCCCCCSRRR...................................DRDDK",
            "........SCCCKRSRRR...........................KRRRK.....K",
            ".........SCCCSKKDDRRRKDRR...............RSRRRKK",
            "..........CCCCCCCCKKKKDRKKKDKRR....RRRRRKKKKS",
            "..........XCCSSCCCCCCCCRKRRRDKKKKKKKSCCCC",
            "...........CCCRRCCCCCCCSKRRRDSCCCCCCCC",
            "............S..CCCCCCCCRDRRRSSCCC",
            "..................CCCCSKRKRR",
            "..................XXX....DRRR",
            "..................DDX......RRR",
            "..................DDD.......DDR",
            ".................DDD.........R",
            "...............SSD..........RR"
        ];
}

static int GetReferenceDinoWidth()
{
    return GetReferenceDinoRows(stride: false).Max(row => row.Length);
}

static void PrintHalfBlockPixel(char topPixel, char bottomPixel)
{
    var topColor = GetPixelColor(topPixel);
    var bottomColor = GetPixelColor(bottomPixel);

    if (topColor is null && bottomColor is null)
    {
        Console.Write("\x1b[0m");
        Console.Write(' ');
        return;
    }

    if (topColor is not null && bottomColor is not null)
    {
        Console.Write($"\x1b[38;2;{topColor.Value.R};{topColor.Value.G};{topColor.Value.B}m");
        Console.Write($"\x1b[48;2;{bottomColor.Value.R};{bottomColor.Value.G};{bottomColor.Value.B}m");
        Console.Write('\u2580');
        return;
    }

    var color = topColor ?? bottomColor;
    Console.Write("\x1b[0m");
    Console.Write($"\x1b[38;2;{color!.Value.R};{color.Value.G};{color.Value.B}m");
    Console.Write(topColor is null ? '\u2584' : '\u2580');
}

static (int R, int G, int B)? GetPixelColor(char pixel)
{
    return pixel switch
    {
        'R' => (196, 61, 57),
        'D' => (117, 36, 39),
        'C' => (238, 220, 181),
        'S' => (188, 154, 126),
        'X' => (165, 119, 98),
        'K' => (42, 30, 31),
        _ => null
    };
}

internal static class ConsoleNative
{
    internal const int StdOutputHandle = -11;
    internal const int EnableVirtualTerminalProcessing = 0x0004;
    internal static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
}


