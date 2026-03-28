using CopilotCliWrapper.Models;

namespace CopilotCliWrapper;

/// <summary>
/// Recognises special wrapper commands entered by the user so they are not
/// forwarded to the native Copilot CLI.
/// </summary>
public static class CommandParser
{
    private static readonly HashSet<string> SimpleCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "history", "clear", "exit", "help"
    };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="input"/> is a built-in wrapper
    /// command rather than a prompt for the native CLI.
    /// </summary>
    public static bool IsWrapperCommand(string input) =>
        TryParse(input, out _);

    /// <summary>
    /// Tries to parse a wrapper command from raw user input.
    /// </summary>
    /// <param name="input">Raw user input string.</param>
    /// <param name="result">
    /// A tuple of (command, argument) when parsing succeeds; otherwise
    /// <c>(null, null)</c>.
    /// </param>
    public static bool TryParse(string input, out (string Command, string? Argument) result)
    {
        result = (string.Empty, null);

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? parts[1].Trim() : null;

        if (SimpleCommands.Contains(command) ||
            command == "search" ||
            command == "export" ||
            command == "model")
        {
            result = (command, argument);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses an export format string, defaulting to Markdown.
    /// </summary>
    public static ExportFormat ParseExportFormat(string? format) =>
        format?.ToLowerInvariant() switch
        {
            "json" => ExportFormat.Json,
            "csv"  => ExportFormat.Csv,
            _      => ExportFormat.Markdown
        };
}
