using CopilotCliWrapper.Models;

namespace CopilotCliWrapper;

/// <summary>
/// Orchestrates the interactive and non-interactive modes of the wrapper,
/// wiring together <see cref="CliExecutor"/>, <see cref="LogManager"/>, and
/// <see cref="CommandParser"/>.
/// </summary>
public class CopilotCliWrapper
{
    private readonly CliExecutor _executor;
    private readonly LogManager _logManager;
    private readonly SessionInfo _session;
    private readonly List<ConversationEntry> _history = new();
    private int _entryCounter;

    public CopilotCliWrapper(
        CliExecutor? executor = null,
        LogManager? logManager = null,
        SessionInfo? session = null)
    {
        _executor = executor ?? new CliExecutor();
        _logManager = logManager ?? new LogManager();
        _session = session ?? new SessionInfo();
    }

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>Read-only view of the in-memory conversation history.</summary>
    public IReadOnlyList<ConversationEntry> History => _history;

    /// <summary>Current model in use.</summary>
    public string CurrentModel => _session.Model;

    /// <summary>
    /// Runs the wrapper in interactive (REPL) mode, reading questions from
    /// stdin and forwarding them to the native Copilot CLI.
    /// </summary>
    public async Task RunInteractiveAsync(CancellationToken cancellationToken = default)
    {
        PrintWelcome();
        _logManager.WriteSessionHeader(_session);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nUse 'exit' to quit gracefully.");
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\n> ");
            var input = Console.ReadLine();

            if (input is null)
                break;

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (CommandParser.TryParse(input, out var parsed))
            {
                var shouldExit = await HandleWrapperCommandAsync(parsed.Command, parsed.Argument);
                if (shouldExit)
                    break;
            }
            else
            {
                await ExecuteAndLogAsync(input, cancellationToken);
            }
        }

        await ShutdownAsync();
    }

    /// <summary>
    /// Runs a single non-interactive query and logs the result.
    /// </summary>
    public async Task RunSingleQueryAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(model))
            _session.Model = model;

        _logManager.WriteSessionHeader(_session);
        await ExecuteAndLogAsync(prompt, cancellationToken);
        _logManager.WriteSessionSummary(_history);
    }

    /// <summary>
    /// Exports the current history in the specified format.
    /// </summary>
    public string ExportHistory(ExportFormat format)
    {
        var path = _logManager.Export(_history, format);
        return path;
    }

    /// <summary>
    /// Searches the history for entries whose question or answer contains
    /// <paramref name="term"/> (case-insensitive).
    /// </summary>
    public IEnumerable<ConversationEntry> Search(string term) =>
        _history.Where(e =>
            e.Question.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            e.Answer.Contains(term, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    private async Task ExecuteAndLogAsync(string prompt, CancellationToken cancellationToken)
    {
        var args = BuildCliArguments(prompt);
        CliExecutionResult result;

        try
        {
            result = await _executor.RunAsync(args, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[wrapper] Error executing CLI: {ex.Message}");
            return;
        }

        var entry = new ConversationEntry
        {
            Id = ++_entryCounter,
            Question = prompt,
            Answer = result.Output,
            Model = _session.Model
        };

        _history.Add(entry);

        DateTime? loopDetectedAt = result.LoopDetected ? DateTime.UtcNow : null;
        _logManager.WriteEntry(entry, loopDetectedAt);
    }

    private string BuildCliArguments(string prompt)
    {
        var modelFlag = _session.Model != "default" ? $"-m {_session.Model} " : string.Empty;
        return $"{modelFlag}-p \"{EscapeForShell(prompt)}\"";
    }

    private static string EscapeForShell(string value) =>
        value.Replace("\"", "\\\"");

    private async Task<bool> HandleWrapperCommandAsync(string command, string? argument)
    {
        switch (command)
        {
            case "exit":
                return true;

            case "help":
                PrintHelp();
                break;

            case "history":
                PrintHistory();
                break;

            case "clear":
                _history.Clear();
                _entryCounter = 0;
                Console.WriteLine("[wrapper] History cleared.");
                break;

            case "search":
                if (string.IsNullOrWhiteSpace(argument))
                {
                    Console.WriteLine("Usage: search <term>");
                }
                else
                {
                    var results = Search(argument).ToList();
                    if (results.Count == 0)
                    {
                        Console.WriteLine($"No results found for '{argument}'.");
                    }
                    else
                    {
                        Console.WriteLine($"Found {results.Count} result(s):");
                        foreach (var r in results)
                        {
                            Console.WriteLine($"  #{r.Id} [{r.Timestamp:HH:mm:ss}] {r.Question}");
                        }
                    }
                }
                break;

            case "export":
                var format = CommandParser.ParseExportFormat(argument);
                var path = ExportHistory(format);
                Console.WriteLine($"[wrapper] Exported to: {path}");
                break;

            case "model":
                if (string.IsNullOrWhiteSpace(argument))
                {
                    Console.WriteLine($"Current model: {_session.Model}");
                }
                else
                {
                    _session.Model = argument;
                    Console.WriteLine($"[wrapper] Model changed to: {_session.Model}");
                }
                break;
        }

        return false;
    }

    private async Task ShutdownAsync()
    {
        _logManager.WriteSessionSummary(_history);
        Console.WriteLine();
        Console.WriteLine("📊 Session Summary");
        Console.WriteLine($"  Total Q&A: {_history.Count}");
        Console.WriteLine($"  Log saved: {_logManager.LogFilePath}");
    }

    private void PrintHistory()
    {
        if (_history.Count == 0)
        {
            Console.WriteLine("No history yet.");
            return;
        }

        Console.WriteLine($"\n📜 History ({_history.Count} entries):");
        foreach (var e in _history)
        {
            Console.WriteLine($"  #{e.Id} [{e.Timestamp:HH:mm:ss}] [{e.Model}] {e.Question}");
        }
    }

    private static void PrintWelcome()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        GitHub Copilot CLI Wrapper v1.0           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.WriteLine("Type your prompt or a wrapper command. Type 'help' for help.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Wrapper commands:");
        Console.WriteLine("  history             – show conversation history");
        Console.WriteLine("  search <term>       – search history");
        Console.WriteLine("  export [json|csv|md]– export history");
        Console.WriteLine("  model [name]        – get or set current model");
        Console.WriteLine("  clear               – clear in-memory history");
        Console.WriteLine("  exit                – exit and show summary");
        Console.WriteLine("  help                – show this message");
        Console.WriteLine();
        Console.WriteLine("Any other input is sent directly to the Copilot CLI.");
    }
}
