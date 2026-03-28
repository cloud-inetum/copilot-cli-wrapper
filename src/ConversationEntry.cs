namespace CopilotCliWrapper;

/// <summary>
/// Represents a single question/answer exchange captured from the CLI.
/// </summary>
public class ConversationEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
