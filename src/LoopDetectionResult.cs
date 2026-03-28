namespace CopilotCliWrapper;

/// <summary>
/// Carries the result of a single loop-detection check performed by
/// <see cref="SmartLoopDetector"/>.
/// </summary>
public class LoopDetectionResult
{
    /// <summary>Whether a repeating cycle was detected.</summary>
    public bool IsLooping { get; set; }

    /// <summary>Number of complete cycles observed so far.</summary>
    public int CycleCount { get; set; }

    /// <summary>Length of the detected cycle in lines.</summary>
    public int CycleLength { get; set; }

    /// <summary>The last line captured before the loop started.</summary>
    public string LastValidLine { get; set; } = string.Empty;

    /// <summary>Confidence score between 0 and 1 (1 = certain).</summary>
    public double Confidence { get; set; }

    /// <summary>
    /// The accumulated output captured <em>before</em> the looping began,
    /// i.e. the valid response that should be saved to the log.
    /// </summary>
    public string ValidResponse { get; set; } = string.Empty;
}
