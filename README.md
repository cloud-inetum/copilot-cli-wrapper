# copilot-cli-wrapper

A C# wrapper for the [GitHub Copilot CLI](https://githubnext.com/projects/copilot-cli/)
that preserves **100% of the native CLI's functionality** while adding automatic
logging, session history, and multi-format export.

## Features

| Feature | Description |
|---------|-------------|
| **Full CLI passthrough** | All arguments, flags, and interactive features of the native CLI work unchanged |
| **Auto-logging** | Every Q&A pair is saved to a timestamped Markdown file |
| **In-memory history** | `history`, `search`, `clear` commands in interactive mode |
| **Multi-format export** | Export to JSON, CSV, or Markdown with `export <format>` |
| **Model switching** | Change models mid-session with `model <name>` |
| **Cross-platform** | Windows, macOS, Linux – detects the correct shell automatically |

## Quick start

```bash
# Prerequisites: .NET 8 SDK + GitHub Copilot CLI installed & authenticated
npm install -g @github/copilot-cli
github-copilot-cli auth

# Run in interactive mode
dotnet run --project src

# Single question
dotnet run --project src -- -p "How do I fix a merge conflict?"

# Specify model
dotnet run --project src -- -m sonnet-4.6 -p "Explain async/await"

# Show help
dotnet run --project src -- -h
```

## Interactive commands

| Command | Description |
|---------|-------------|
| `history` | Show Q&A history for this session |
| `search <term>` | Search history |
| `export [json\|csv\|md]` | Export history to a file |
| `model [name]` | Show or change the active model |
| `clear` | Clear in-memory history |
| `exit` | Exit with session summary |
| `help` | Show help |

Any other input is forwarded directly to the Copilot CLI.

## Documentation

- [Usage guide](docs/USAGE.md)
- [API reference](docs/API.md)
- [Architecture](docs/ARCHITECTURE.md)

## Project structure

```
src/
├── Program.cs               # Entry point & CLI argument parsing
├── CopilotCliWrapper.cs     # Main orchestrator / REPL loop
├── ConversationEntry.cs     # Q&A data model
├── LogManager.cs            # Session logging & exports
├── CommandParser.cs         # Wrapper command parser
├── CliExecutor.cs           # Native CLI executor (cross-platform)
└── Models/
    ├── SessionInfo.cs       # Session metadata
    └── ExportFormat.cs      # Export format enum

tests/
└── CopilotCliWrapperTests.cs  # xUnit test suite

docs/
├── USAGE.md
├── API.md
└── ARCHITECTURE.md
```

## Running tests

```bash
dotnet test tests/CopilotCliWrapper.Tests.csproj
```

## License

MIT
