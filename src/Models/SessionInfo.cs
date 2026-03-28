namespace CopilotCliWrapper.Models;

/// <summary>
/// Holds metadata about the current wrapper session.
/// </summary>
public class SessionInfo
{
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public string Model { get; set; } = "default";
    public string Username { get; init; } = Environment.UserName;
    public string Platform { get; init; } = GetPlatformName();

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Linux";
    }
}
