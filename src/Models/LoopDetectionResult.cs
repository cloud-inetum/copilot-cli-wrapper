namespace CopilotCliWrapper.Models;

/// <summary>
/// Result returned by <see cref="SmartLoopDetector"/> after analysing a line
/// of CLI output.
/// </summary>
public class LoopDetectionResult
{
    /// <summary>Whether an infinite loop was detected.</summary>
    public bool IsLooping { get; set; }

    /// <summary>Number of complete cycles observed.</summary>
    public int CycleCount { get; set; }

    /// <summary>Length of the repeating cycle in lines.</summary>
    public int CycleLength { get; set; }

    /// <summary>Last line captured before the loop began.</summary>
    public string LastValidLine { get; set; } = string.Empty;

    /// <summary>Detection confidence between 0 (none) and 1 (certain).</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The accumulated output prior to (and not including) the looping
    /// portion, suitable for saving to the log.
    /// </summary>
    public string ValidResponse { get; set; } = string.Empty;

    /// <summary>The normalised pattern that was found to be repeating.</summary>
    public string RepeatingPattern { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index in the internal buffer where looping was first
    /// detected.
    /// </summary>
    public int LoopStartPosition { get; set; }
}
