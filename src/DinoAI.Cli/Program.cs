using DinoAI.Core.Sessions;

var sessions = new InMemoryAgentSessionStore();

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

Console.WriteLine("DinoAI CLI");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  dino new [title]");
Console.WriteLine("  dino demo");
