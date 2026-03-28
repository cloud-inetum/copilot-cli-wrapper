using CopilotCliWrapper;
using CopilotCliWrapper.Models;

// ── Parse command-line arguments ────────────────────────────────────────────

string? prompt = null;
string? model = null;
string? exportArg = null;
string? searchArg = null;
string? cliPath = null;
bool showHelp = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-p" or "--prompt" when i + 1 < args.Length:
            prompt = args[++i];
            break;

        case "-m" or "--model" when i + 1 < args.Length:
            model = args[++i];
            break;

        case "--export" when i + 1 < args.Length:
            exportArg = args[++i];
            break;

        case "--search" when i + 1 < args.Length:
            searchArg = args[++i];
            break;

        case "--cli-path" when i + 1 < args.Length:
            cliPath = args[++i];
            break;

        case "-h" or "--help":
            showHelp = true;
            break;

        default:
            if (prompt is null)
                prompt = args[i];
            break;
    }
}

if (showHelp)
{
    PrintHelp();
    return 0;
}

// ── Build wrapper ────────────────────────────────────────────────────────────

var session = new SessionInfo { Model = model ?? "default" };
var executor = new CliExecutor(cliPath);
var logManager = new LogManager();
var wrapper = new CopilotCliWrapper.CopilotCliWrapper(executor, logManager, session);

// ── Non-interactive export / search shortcuts ────────────────────────────────

if (exportArg is not null)
{
    var format = CommandParser.ParseExportFormat(exportArg);
    var path = wrapper.ExportHistory(format);
    Console.WriteLine($"Exported to: {path}");
    return 0;
}

if (searchArg is not null)
{
    var results = wrapper.Search(searchArg).ToList();
    Console.WriteLine($"Found {results.Count} result(s) for '{searchArg}':");
    foreach (var r in results)
        Console.WriteLine($"  #{r.Id} [{r.Timestamp:HH:mm:ss}] {r.Question}");
    return 0;
}

// ── Non-interactive single prompt ────────────────────────────────────────────

if (prompt is not null)
{
    await wrapper.RunSingleQueryAsync(prompt, model);
    return 0;
}

// ── Interactive mode ─────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await wrapper.RunInteractiveAsync(cts.Token);
return 0;

// ── Local helpers ─────────────────────────────────────────────────────────────

static void PrintHelp()
{
    Console.WriteLine("GitHub Copilot CLI Wrapper");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run                         Interactive mode");
    Console.WriteLine("  dotnet run -- -p \"question\"        Single question");
    Console.WriteLine("  dotnet run -- -m <model>           Set model");
    Console.WriteLine("  dotnet run -- --export json|csv|md Export history");
    Console.WriteLine("  dotnet run -- --search <term>      Search history");
    Console.WriteLine("  dotnet run -- --cli-path <path>    Override CLI path");
    Console.WriteLine("  dotnet run -- -h                   Show this help");
}
