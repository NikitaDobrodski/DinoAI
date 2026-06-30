# DinoAI Architecture

DinoAI should keep product UI separate from agent runtime logic.

## Runtime shape

```text
DinoAI.Core
  Sessions
  Messages
  Tool registry
  Permission gates
  Workspace operations
  Model provider abstractions

DinoAI.Server
  Minimal API
  Streaming events
  Session endpoints
  Tool execution endpoints

DinoAI.Web
  Blazor chat UI
  Workspace tree
  Diff viewer
  Approval prompts

DinoAI.Cli
  dino chat
  dino run
  dino serve
```

## Early design rules

- Core must not depend on Blazor or ASP.NET Core hosting details.
- Tools must be explicit and permission-aware.
- Workspace writes should go through patch-oriented operations.
- The first model provider should use an OpenAI-compatible chat API.
- .NET-specific capabilities should become DinoAI's advantage: solution discovery, project graph, Roslyn diagnostics, tests, and NuGet awareness.
