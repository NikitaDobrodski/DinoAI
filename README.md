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
