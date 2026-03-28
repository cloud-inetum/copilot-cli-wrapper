using System.Text;
using System.Text.Json;
using CopilotCliWrapper.Models;

namespace CopilotCliWrapper;

/// <summary>
/// Persists conversation history to a Markdown log file and supports
/// exporting in JSON, CSV, and Markdown formats.
/// </summary>
public class LogManager
{
    private readonly string _logDirectory;
    private readonly string _logFilePath;

    public LogManager(string? logDirectory = null)
    {
        _logDirectory = logDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".copilot-wrapper", "logs");

        Directory.CreateDirectory(_logDirectory);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFilePath = Path.Combine(_logDirectory, $"session_{timestamp}.md");
    }

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>Path of the active log file.</summary>
    public string LogFilePath => _logFilePath;

    /// <summary>
    /// Writes the session header to the log file.
    /// </summary>
    public void WriteSessionHeader(SessionInfo session)
    {
        var header = new StringBuilder();
        header.AppendLine("---");
        header.AppendLine();
        header.AppendLine($"## 📅 Sessão: {session.StartedAt:yyyy-MM-dd HH:mm:ss}");
        header.AppendLine($"**Modelo:** {session.Model}");
        header.AppendLine($"**Usuário:** {session.Username}");
        header.AppendLine($"**Plataforma:** {session.Platform}");
        header.AppendLine();
        AppendToLog(header.ToString());
    }

    /// <summary>
    /// Appends a Q&amp;A entry to the log file.  When <paramref name="loopDetectedAt"/>
    /// is provided the entry is marked with a loop-detection notice and the
    /// timestamp of detection.
    /// </summary>
    public void WriteEntry(ConversationEntry entry, DateTime? loopDetectedAt = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### ❓ Pergunta");
        sb.AppendLine($"> {entry.Question}");
        sb.AppendLine();
        sb.AppendLine("### ✅ Resposta");
        sb.AppendLine(entry.Answer.TrimEnd());
        sb.AppendLine();
        sb.AppendLine($"**🕐 {entry.Timestamp:HH:mm:ss}** | **Model:** {entry.Model}");

        if (loopDetectedAt.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine($"> ⚠️ **loop_detected: true** | Detected at: {loopDetectedAt.Value:HH:mm:ss.fff}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        AppendToLog(sb.ToString());
    }

    /// <summary>
    /// Appends a session summary to the log file.
    /// </summary>
    public void WriteSessionSummary(IReadOnlyList<ConversationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 📊 Resumo da Sessão");
        sb.AppendLine($"- **Total de Q&A:** {entries.Count}");
        sb.AppendLine($"- **Encerrada em:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        AppendToLog(sb.ToString());
    }

    /// <summary>
    /// Exports conversation entries to the requested format and returns the
    /// path of the generated file.
    /// </summary>
    public string Export(IReadOnlyList<ConversationEntry> entries, ExportFormat format)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var extension = format switch
        {
            ExportFormat.Json     => "json",
            ExportFormat.Csv      => "csv",
            ExportFormat.Markdown => "md",
            _                     => "md"
        };

        var exportPath = Path.Combine(_logDirectory, $"export_{timestamp}.{extension}");

        var content = format switch
        {
            ExportFormat.Json     => BuildJsonExport(entries),
            ExportFormat.Csv      => BuildCsvExport(entries),
            ExportFormat.Markdown => BuildMarkdownExport(entries),
            _                     => BuildMarkdownExport(entries)
        };

        File.WriteAllText(exportPath, content, Encoding.UTF8);
        return exportPath;
    }

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    private void AppendToLog(string content) =>
        File.AppendAllText(_logFilePath, content, Encoding.UTF8);

    private static string BuildJsonExport(IReadOnlyList<ConversationEntry> entries)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var data = entries.Select(e => new
        {
            e.Id,
            Timestamp = e.Timestamp.ToString("o"),
            e.Question,
            e.Answer,
            e.Model
        });
        return JsonSerializer.Serialize(data, options);
    }

    private static string BuildCsvExport(IReadOnlyList<ConversationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Timestamp,Model,Question,Answer");
        foreach (var e in entries)
        {
            sb.AppendLine(
                $"{e.Id},{e.Timestamp:o},{CsvEscape(e.Model)},{CsvEscape(e.Question)},{CsvEscape(e.Answer)}");
        }
        return sb.ToString();
    }

    private static string BuildMarkdownExport(IReadOnlyList<ConversationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Histórico de Conversa");
        sb.AppendLine();
        foreach (var e in entries)
        {
            sb.AppendLine($"### ❓ Pergunta #{e.Id}");
            sb.AppendLine($"> {e.Question}");
            sb.AppendLine();
            sb.AppendLine("### ✅ Resposta");
            sb.AppendLine(e.Answer.TrimEnd());
            sb.AppendLine();
            sb.AppendLine($"**🕐 {e.Timestamp:HH:mm:ss}** | **Model:** {e.Model}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string CsvEscape(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
