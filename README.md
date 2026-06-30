# DinoAI

DinoAI is a C#/.NET local AI coding agent inspired by OpenCode, with a Blazor-first interface and a .NET-native workspace runtime.

## Project layout

- `src/DinoAI.Core` - agent domain, sessions, tools, permissions, model abstractions.
- `src/DinoAI.Server` - local ASP.NET Core API for agent clients.
- `src/DinoAI.Web` - Blazor UI for chat, workspace context, diffs, and approvals.
- `src/DinoAI.Cli` - command-line entry point for chat, run, and serve workflows.

## MVP goal

Build the first useful loop:

1. User sends a task.
2. Agent reads and searches the workspace.
3. Agent proposes or applies a patch.
4. Agent runs checks.
5. User reviews the result and diff.

## Workspace tools

DinoAI.Core now includes a guarded filesystem workspace service. It can describe a root, find files by pattern, and read files by relative path while blocking paths that escape the workspace root.

CLI examples:

```powershell
dotnet run --project src/DinoAI.Cli -- workspace D:\DinoAI
dotnet run --project src/DinoAI.Cli -- files D:\DinoAI *.csproj
dotnet run --project src/DinoAI.Cli -- read D:\DinoAI README.md
```

## Tool registry

Workspace operations are also exposed as agent tools:

- `workspace.describe`
- `workspace.find_files`
- `workspace.read_file`

CLI examples:

```powershell
dotnet run --project src/DinoAI.Cli -- tools
dotnet run --project src/DinoAI.Cli -- tool workspace.find_files D:\DinoAI pattern=*.csproj maxResults=20
dotnet run --project src/DinoAI.Cli -- tool workspace.read_file D:\DinoAI path=README.md
```
