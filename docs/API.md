# API Documentation

## Namespace: `CopilotCliWrapper`

---

### `CopilotCliWrapper` class

The main orchestrator.  Wires together `CliExecutor`, `LogManager`, and
`CommandParser`.

#### Constructor

```csharp
public CopilotCliWrapper(
    CliExecutor?  executor   = null,
    LogManager?   logManager = null,
    SessionInfo?  session    = null)
```

All parameters are optional; sensible defaults are created when omitted.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `History` | `IReadOnlyList<ConversationEntry>` | In-memory Q&A pairs for the current session |
| `CurrentModel` | `string` | Model currently active for CLI calls |

#### Methods

```csharp
// Start interactive REPL mode
Task RunInteractiveAsync(CancellationToken cancellationToken = default)

// Execute a single query and log it
Task RunSingleQueryAsync(string prompt, string? model = null,
    CancellationToken cancellationToken = default)

// Export history and return the output file path
string ExportHistory(ExportFormat format)

// Search history by keyword (case-insensitive)
IEnumerable<ConversationEntry> Search(string term)
```

---

### `CliExecutor` class

Locates the native Copilot CLI binary and executes it, capturing combined
stdout/stderr output.

#### Constructor

```csharp
public CliExecutor(string? cliPath = null)
```

When `cliPath` is `null` the executor searches well-known installation paths
and falls back to the system `PATH`.  Set `COPILOT_CLI_PATH` to override.

#### Methods

```csharp
// Execute CLI with the given arguments; streams output to console
Task<string> RunAsync(string arguments, CancellationToken cancellationToken = default)

// Returns true when the CLI binary is found
bool IsInstalled()
```

---

### `LogManager` class

Writes Markdown session logs and supports multi-format exports.

#### Constructor

```csharp
public LogManager(string? logDirectory = null)
```

Defaults to `~/.copilot-wrapper/logs/`.

#### Methods

```csharp
void WriteSessionHeader(SessionInfo session)
void WriteEntry(ConversationEntry entry)
void WriteSessionSummary(IReadOnlyList<ConversationEntry> entries)

// Returns the path of the generated export file
string Export(IReadOnlyList<ConversationEntry> entries, ExportFormat format)
```

---

### `CommandParser` static class

Identifies wrapper built-in commands vs. CLI prompts.

```csharp
static bool IsWrapperCommand(string input)
static bool TryParse(string input, out (string Command, string? Argument) result)
static ExportFormat ParseExportFormat(string? format)
```

---

## Namespace: `CopilotCliWrapper.Models`

### `SessionInfo`

```csharp
public class SessionInfo
{
    public DateTime StartedAt { get; init; }
    public string   Model     { get; set;  }   // mutable – can be changed mid-session
    public string   Username  { get; init; }
    public string   Platform  { get; init; }   // "Windows" | "macOS" | "Linux"
}
```

### `ExportFormat` enum

```csharp
public enum ExportFormat { Json, Csv, Markdown }
```

---

## Namespace: `CopilotCliWrapper` (model)

### `ConversationEntry`

```csharp
public class ConversationEntry
{
    public int      Id        { get; set;  }
    public DateTime Timestamp { get; init; }
    public string   Question  { get; init; }
    public string   Answer    { get; init; }
    public string   Model     { get; init; }
}
```
