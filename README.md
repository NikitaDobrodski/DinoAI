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

## Local agent runner

DinoAI now has a deterministic local agent runner. It does not call an LLM yet; it plans simple workspace tool calls from commands or intent-like messages and writes user, tool, and assistant messages into the session.

Try:

```powershell
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /workspace
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /files *.csproj
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /read README.md
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI show project files
```

## Permissions and writes

DinoAI has a first permission layer. Read-only workspace tools are allowed by default. Workspace writes require explicit approval.

Example blocked write:

```powershell
dotnet run --project src/DinoAI.Cli -- tool workspace.write_file D:\DinoAI path=.tmp/permission-test.txt content=hello
```

Example approved write:

```powershell
dotnet run --project src/DinoAI.Cli -- tool workspace.write_file D:\DinoAI path=.tmp/permission-test.txt content=hello confirm=true overwrite=true
```

## Shell tool

DinoAI includes a permission-aware shell tool. A small allowlist can run without explicit approval: `dotnet build`, `dotnet test`, `dotnet --info`, `git status`, `git diff`, and `git log`. Other commands return `Permission Ask` unless called with `confirm=true`.

Examples:

```powershell
dotnet run --project src/DinoAI.Cli -- tool shell.run D:\DinoAI command="git status --short"
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /status
dotnet run --project src/DinoAI.Cli -- ask D:\DinoAI /build
```

## Persistent sessions

Sessions are stored in `.dinoai/sessions.json` under the workspace root. The `.dinoai/` directory is local runtime state and is ignored by git.
