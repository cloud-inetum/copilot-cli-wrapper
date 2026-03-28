using CopilotCliWrapper;
using CopilotCliWrapper.Models;

namespace CopilotCliWrapper.Tests;

public class CommandParserTests
{
    [Theory]
    [InlineData("history")]
    [InlineData("HISTORY")]
    [InlineData("exit")]
    [InlineData("clear")]
    [InlineData("help")]
    public void IsWrapperCommand_RecognisesSimpleCommands(string input)
    {
        Assert.True(CommandParser.IsWrapperCommand(input));
    }

    [Theory]
    [InlineData("search oauth")]
    [InlineData("export json")]
    [InlineData("model sonnet-4.6")]
    public void IsWrapperCommand_RecognisesParametrisedCommands(string input)
    {
        Assert.True(CommandParser.IsWrapperCommand(input));
    }

    [Theory]
    [InlineData("how do I fix this bug?")]
    [InlineData("copilot explain ./src/error.log")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsWrapperCommand_ReturnsFalseForCliPrompts(string input)
    {
        Assert.False(CommandParser.IsWrapperCommand(input));
    }

    [Fact]
    public void TryParse_ExtractsCommandAndArgument()
    {
        var result = CommandParser.TryParse("search oauth", out var parsed);

        Assert.True(result);
        Assert.Equal("search", parsed.Command);
        Assert.Equal("oauth", parsed.Argument);
    }

    [Fact]
    public void TryParse_HandlesCommandWithNoArgument()
    {
        var result = CommandParser.TryParse("history", out var parsed);

        Assert.True(result);
        Assert.Equal("history", parsed.Command);
        Assert.Null(parsed.Argument);
    }

    [Theory]
    [InlineData("json", ExportFormat.Json)]
    [InlineData("JSON", ExportFormat.Json)]
    [InlineData("csv", ExportFormat.Csv)]
    [InlineData("md", ExportFormat.Markdown)]
    [InlineData("markdown", ExportFormat.Markdown)]
    [InlineData(null, ExportFormat.Markdown)]
    [InlineData("unknown", ExportFormat.Markdown)]
    public void ParseExportFormat_ReturnsCorrectFormat(string? input, ExportFormat expected)
    {
        Assert.Equal(expected, CommandParser.ParseExportFormat(input));
    }
}

public class ConversationEntryTests
{
    [Fact]
    public void ConversationEntry_DefaultTimestampIsNow()
    {
        var before = DateTime.Now;
        var entry = new ConversationEntry();
        var after = DateTime.Now;

        Assert.InRange(entry.Timestamp, before, after);
    }

    [Fact]
    public void ConversationEntry_PropertiesCanBeInitialised()
    {
        var ts = new DateTime(2026, 3, 28, 14, 30, 0);
        var entry = new ConversationEntry
        {
            Id = 1,
            Question = "What is OAuth?",
            Answer = "OAuth is an open standard...",
            Model = "sonnet-4.6"
        };

        Assert.Equal(1, entry.Id);
        Assert.Equal("What is OAuth?", entry.Question);
        Assert.Equal("OAuth is an open standard...", entry.Answer);
        Assert.Equal("sonnet-4.6", entry.Model);
    }
}

public class LogManagerTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly LogManager _logManager;

    public LogManagerTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), $"copilot-wrapper-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tmpDir);
        _logManager = new LogManager(_tmpDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    [Fact]
    public void LogFilePath_IsInsideLogDirectory()
    {
        Assert.StartsWith(_tmpDir, _logManager.LogFilePath);
        Assert.EndsWith(".md", _logManager.LogFilePath);
    }

    [Fact]
    public void WriteSessionHeader_CreatesFile()
    {
        var session = new SessionInfo { Model = "sonnet-4.6" };
        _logManager.WriteSessionHeader(session);

        Assert.True(File.Exists(_logManager.LogFilePath));
        var content = File.ReadAllText(_logManager.LogFilePath);
        Assert.Contains("sonnet-4.6", content);
        Assert.Contains("Sessão:", content);
    }

    [Fact]
    public void WriteEntry_AppendsQandA()
    {
        var entry = new ConversationEntry
        {
            Id = 1,
            Question = "What is OAuth?",
            Answer = "OAuth is an open standard.",
            Model = "sonnet-4.6"
        };

        _logManager.WriteEntry(entry);

        var content = File.ReadAllText(_logManager.LogFilePath);
        Assert.Contains("What is OAuth?", content);
        Assert.Contains("OAuth is an open standard.", content);
    }

    [Fact]
    public void Export_Json_CreatesValidJsonFile()
    {
        var entries = new List<ConversationEntry>
        {
            new() { Id = 1, Question = "Q1", Answer = "A1", Model = "test-model" }
        };

        var path = _logManager.Export(entries, ExportFormat.Json);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("Q1", content);
        Assert.Contains("A1", content);
        // Validate it's valid JSON
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Export_Csv_CreatesFileWithHeader()
    {
        var entries = new List<ConversationEntry>
        {
            new() { Id = 1, Question = "Q1", Answer = "A1", Model = "test-model" }
        };

        var path = _logManager.Export(entries, ExportFormat.Csv);

        Assert.True(File.Exists(path));
        var lines = File.ReadAllLines(path);
        Assert.Equal("Id,Timestamp,Model,Question,Answer", lines[0]);
        Assert.Contains("Q1", lines[1]);
    }

    [Fact]
    public void Export_Markdown_ContainsEntrySections()
    {
        var entries = new List<ConversationEntry>
        {
            new() { Id = 1, Question = "Q1", Answer = "A1", Model = "test-model" }
        };

        var path = _logManager.Export(entries, ExportFormat.Markdown);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("Pergunta", content);
        Assert.Contains("Resposta", content);
        Assert.Contains("Q1", content);
    }
}

public class CopilotCliWrapperSearchTests
{
    [Fact]
    public void Search_FindsMatchingEntries()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"wrapper-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tmpDir);

        try
        {
            var logManager = new LogManager(tmpDir);
            var session = new SessionInfo();
            var wrapper = new CopilotCliWrapper(null!, logManager, session);

            // Inject entries directly via reflection to avoid needing real CLI
            var historyField = typeof(CopilotCliWrapper)
                .GetField("_history", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var history = (List<ConversationEntry>)historyField.GetValue(wrapper)!;
            history.Add(new ConversationEntry { Id = 1, Question = "OAuth flow", Answer = "OAuth is...", Model = "test" });
            history.Add(new ConversationEntry { Id = 2, Question = "SAML vs OAuth", Answer = "SAML is...", Model = "test" });
            history.Add(new ConversationEntry { Id = 3, Question = "Docker basics", Answer = "Docker is...", Model = "test" });

            var results = wrapper.Search("oauth").ToList();

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Contains("oauth", r.Question, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void Search_ReturnsEmptyWhenNoMatch()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"wrapper-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tmpDir);

        try
        {
            var logManager = new LogManager(tmpDir);
            var wrapper = new CopilotCliWrapper(null!, logManager, new SessionInfo());

            var results = wrapper.Search("nonexistent-xyz").ToList();

            Assert.Empty(results);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }
}

public class SessionInfoTests
{
    [Fact]
    public void SessionInfo_PlatformIsNotEmpty()
    {
        var session = new SessionInfo();
        Assert.NotEmpty(session.Platform);
    }

    [Fact]
    public void SessionInfo_ModelDefaultsToDefault()
    {
        var session = new SessionInfo();
        Assert.Equal("default", session.Model);
    }

    [Fact]
    public void SessionInfo_ModelCanBeChanged()
    {
        var session = new SessionInfo { Model = "sonnet-4.6" };
        session.Model = "claude-opus-4.6";
        Assert.Equal("claude-opus-4.6", session.Model);
    }
}
