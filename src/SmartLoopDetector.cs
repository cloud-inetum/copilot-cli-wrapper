using System.Text.RegularExpressions;
using CopilotCliWrapper.Models;

namespace CopilotCliWrapper;

/// <summary>
/// Detects infinite loops in streaming CLI output by maintaining a rolling
/// in-memory buffer of captured lines.
///
/// <para>Two complementary strategies are used:</para>
/// <list type="bullet">
///   <item><description>
///     <b>Exact match</b> – a new line that already exists in the buffer
///     triggers a cycle-length search.  If the cycle repeats at least twice it
///     is confirmed as a loop.
///   </description></item>
///   <item><description>
///     <b>Pattern match</b> – each line is normalised (numbers, timestamps,
///     e-mails, UUIDs and file paths are replaced with placeholders) before
///     the same cycle-length search is applied.  This catches loops where the
///     content varies slightly on every iteration.
///   </description></item>
/// </list>
///
/// <para>
/// Additionally, an output rate above 50 lines/s is treated as suspicious
/// and raises the confidence score even when a clean cycle cannot be found.
/// </para>
/// </summary>
public class SmartLoopDetector
{
    // ------------------------------------------------------------------ //
    //  Constants                                                           //
    // ------------------------------------------------------------------ //

    private const int MaxBufferSize = 1000;
    private const int MinCycleLines = 2;
    private const double HighSpeedThreshold = 50.0; // lines per second
    private const int MinLinesBeforeSpeedCheck = 30; // need enough data for reliable measurement

    // ------------------------------------------------------------------ //
    //  State                                                               //
    // ------------------------------------------------------------------ //

    private readonly List<string> _buffer = new(MaxBufferSize);
    private readonly List<string> _patternBuffer = new(MaxBufferSize);
    private readonly List<DateTime> _timestamps = new(MaxBufferSize);

    private int _cycleCount;

    // ------------------------------------------------------------------ //
    //  Regex patterns used for normalisation                              //
    // ------------------------------------------------------------------ //

    private static readonly Regex _uuidRegex =
        new(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
            RegexOptions.Compiled);

    private static readonly Regex _emailRegex =
        new(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled);

    private static readonly Regex _timestampRegex =
        new(@"\b\d{1,2}:\d{2}(:\d{2})?\b",
            RegexOptions.Compiled);

    private static readonly Regex _pathRegex =
        new(@"(?:[A-Za-z]:\\[^\s""]*|/[^\s""]*)",
            RegexOptions.Compiled);

    private static readonly Regex _numberRegex =
        new(@"\b\d+\b",
            RegexOptions.Compiled);

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Analyses <paramref name="newLine"/> and returns a
    /// <see cref="LoopDetectionResult"/> describing whether looping is
    /// occurring.
    /// </summary>
    public LoopDetectionResult Analyse(string newLine)
    {
        var now = DateTime.UtcNow;

        // Trim the buffer when it reaches the maximum size.
        if (_buffer.Count >= MaxBufferSize)
        {
            _buffer.RemoveAt(0);
            _patternBuffer.RemoveAt(0);
            _timestamps.RemoveAt(0);
        }

        var normalised = NormaliseLine(newLine);

        // --- Strategy 1: exact match ---
        var exactResult = TryDetectCycle(_buffer, newLine, exact: true);
        if (exactResult.IsLooping)
        {
            _cycleCount++;
            exactResult.CycleCount = _cycleCount;
            exactResult.ValidResponse = BuildValidResponse(exactResult.LoopStartPosition);
            exactResult.LastValidLine = _buffer.Count > 0 ? _buffer[^1] : string.Empty;

            _buffer.Add(newLine);
            _patternBuffer.Add(normalised);
            _timestamps.Add(now);

            return exactResult;
        }

        // --- Strategy 2: pattern match ---
        var patternResult = TryDetectCycle(_patternBuffer, normalised, exact: false);
        if (patternResult.IsLooping)
        {
            _cycleCount++;
            patternResult.CycleCount = _cycleCount;
            patternResult.ValidResponse = BuildValidResponse(patternResult.LoopStartPosition);
            patternResult.LastValidLine = _buffer.Count > 0 ? _buffer[^1] : string.Empty;

            _buffer.Add(newLine);
            _patternBuffer.Add(normalised);
            _timestamps.Add(now);

            return patternResult;
        }

        // --- Strategy 3: high output speed ---
        _buffer.Add(newLine);
        _patternBuffer.Add(normalised);
        _timestamps.Add(now);

        var speedResult = CheckHighSpeed();
        if (speedResult.IsLooping)
        {
            _cycleCount++;
            speedResult.CycleCount = _cycleCount;
            speedResult.ValidResponse = BuildValidResponse(speedResult.LoopStartPosition);
            speedResult.LastValidLine = _buffer.Count > 0 ? _buffer[^1] : string.Empty;
            return speedResult;
        }

        return new LoopDetectionResult { IsLooping = false };
    }

    /// <summary>
    /// Returns a snapshot of detector statistics useful for diagnostics.
    /// </summary>
    public (int BufferedLines, int CyclesDetected, double LinesPerSecond) GetStatistics()
    {
        var lps = MeasureLinesPerSecond();
        return (_buffer.Count, _cycleCount, lps);
    }

    /// <summary>
    /// Resets the detector state (buffer, cycle counter, timestamps).
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _patternBuffer.Clear();
        _timestamps.Clear();
        _cycleCount = 0;
    }

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Attempts to find a repeating cycle of lines that ends with
    /// <paramref name="candidate"/> being the first line of a new cycle.
    /// </summary>
    private static LoopDetectionResult TryDetectCycle(
        IList<string> buffer,
        string candidate,
        bool exact)
    {
        if (buffer.Count < MinCycleLines * 2)
            return new LoopDetectionResult { IsLooping = false };

        // Find the most-recent occurrence of candidate in the buffer.
        int firstOccurrence = -1;
        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            if (string.Equals(buffer[i], candidate, StringComparison.Ordinal))
            {
                firstOccurrence = i;
                break;
            }
        }

        if (firstOccurrence < 0)
            return new LoopDetectionResult { IsLooping = false };

        int cycleLen = buffer.Count - firstOccurrence;

        if (cycleLen < MinCycleLines || cycleLen > buffer.Count / 2)
            return new LoopDetectionResult { IsLooping = false };

        // Verify that the sequence before firstOccurrence matches the cycle.
        int verifyStart = firstOccurrence - cycleLen;
        if (verifyStart < 0)
            return new LoopDetectionResult { IsLooping = false };

        bool matches = true;
        for (int i = 0; i < cycleLen; i++)
        {
            if (!string.Equals(buffer[verifyStart + i], buffer[firstOccurrence + i], StringComparison.Ordinal))
            {
                matches = false;
                break;
            }
        }

        if (!matches)
            return new LoopDetectionResult { IsLooping = false };

        double confidence = exact ? 0.95 : 0.85;

        return new LoopDetectionResult
        {
            IsLooping = true,
            CycleLength = cycleLen,
            Confidence = confidence,
            RepeatingPattern = buffer[firstOccurrence],
            LoopStartPosition = firstOccurrence
        };
    }

    /// <summary>
    /// Checks whether lines are arriving faster than the suspicious threshold.
    /// Requires at least <see cref="MinLinesBeforeSpeedCheck"/> buffered lines
    /// before attempting to measure — fewer lines give unreliable estimates.
    /// </summary>
    private LoopDetectionResult CheckHighSpeed()
    {
        if (_timestamps.Count < MinLinesBeforeSpeedCheck)
            return new LoopDetectionResult { IsLooping = false };

        var lps = MeasureLinesPerSecond();
        if (lps <= HighSpeedThreshold)
            return new LoopDetectionResult { IsLooping = false };

        return new LoopDetectionResult
        {
            IsLooping = true,
            Confidence = Math.Min(0.5 + (lps - HighSpeedThreshold) / 200.0, 0.9),
            LoopStartPosition = Math.Max(0, _buffer.Count - 10),
            RepeatingPattern = "(high-speed output)"
        };
    }

    /// <summary>
    /// Measures the current output rate in lines per second using the last
    /// 50 timestamps (or fewer if not enough data exists).
    /// </summary>
    private double MeasureLinesPerSecond()
    {
        if (_timestamps.Count < 2)
            return 0.0;

        int window = Math.Min(50, _timestamps.Count);
        var oldest = _timestamps[_timestamps.Count - window];
        var newest = _timestamps[^1];
        var elapsed = (newest - oldest).TotalSeconds;

        return elapsed > 0 ? (window - 1) / elapsed : 0.0;
    }

    /// <summary>
    /// Builds the valid portion of the captured output up to (but not
    /// including) the loop start position.
    /// </summary>
    private string BuildValidResponse(int loopStartPosition)
    {
        if (loopStartPosition <= 0 || _buffer.Count == 0)
            return string.Empty;

        int end = Math.Min(loopStartPosition, _buffer.Count);
        return string.Join(Environment.NewLine, _buffer.Take(end));
    }

    /// <summary>
    /// Normalises a line by replacing variable tokens with placeholders so
    /// that lines differing only in numbers, timestamps, etc. compare equal.
    /// </summary>
    public static string NormaliseLine(string line)
    {
        // Order matters: UUIDs before plain numbers, timestamps before numbers.
        line = _uuidRegex.Replace(line, "[UUID]");
        line = _emailRegex.Replace(line, "[EMAIL]");
        line = _timestampRegex.Replace(line, "[TIME]");
        line = _pathRegex.Replace(line, "[PATH]");
        line = _numberRegex.Replace(line, "[NUM]");
        return line;
    }
}
