using System.Text.RegularExpressions;

namespace CopilotCliWrapper;

/// <summary>
/// Detects infinite-output loops in CLI output by comparing incoming lines
/// against an in-memory history buffer.  Two detection strategies are used:
///
/// <list type="number">
///   <item>Exact match – the new line was already seen in the recent history
///         <em>and</em> the preceding subsequence matches, confirming that a
///         full cycle is repeating.</item>
///   <item>Pattern match – after normalising variable tokens (numbers,
///         e-mails, timestamps, UUIDs, file paths) the normalised form of
///         the new line has been seen before, so the loop only differs in
///         variable values.</item>
///   <item>High output rate – the CLI is emitting lines far faster than any
///         genuine response would, indicating runaway output.</item>
/// </list>
/// </summary>
public class SmartLoopDetector
{
    private const int MaxBufferSize = 1000;
    private const int MinCycleLength = 2;
    private const double ExactMatchConfidence = 0.95;
    private const double PatternMatchConfidence = 0.80;
    private const double HighOutputRateConfidence = 0.70;
    private const int HighOutputRateThreshold = 50; // lines per second

    private readonly Queue<string> _outputBuffer = new();
    private readonly Queue<string> _patternBuffer = new();
    private int _cycleCount;
    private DateTime _lastLineTime;
    private readonly Queue<DateTime> _recentLineTimes = new(); // sliding window for rate calculation

    public SmartLoopDetector()
    {
        _lastLineTime = DateTime.UtcNow;
    }

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Analyses <paramref name="newLine"/> and returns a
    /// <see cref="LoopDetectionResult"/> indicating whether an infinite loop
    /// has been detected.
    /// </summary>
    public LoopDetectionResult DetectLoop(string newLine)
    {
        int linesPerSecond = UpdateOutputRate();

        // --- Strategy 1: exact-match cycle detection ---
        if (_outputBuffer.Contains(newLine))
        {
            int pos = GetLinePosition(newLine);
            var candidateCycle = _outputBuffer.TakeLast(pos).ToList();

            if (candidateCycle.Count >= MinCycleLength && VerifySequenceRepeat(candidateCycle, newLine))
            {
                _cycleCount++;
                string validResponse = GetResponseBeforeLoop(pos);

                return new LoopDetectionResult
                {
                    IsLooping = true,
                    CycleCount = _cycleCount,
                    CycleLength = pos,
                    LastValidLine = _outputBuffer.LastOrDefault() ?? string.Empty,
                    Confidence = ExactMatchConfidence,
                    ValidResponse = validResponse
                };
            }
        }

        // --- Strategy 2: normalised-pattern cycle detection ---
        string pattern = ExtractPattern(newLine);
        if (_patternBuffer.Contains(pattern))
        {
            int pos = GetPatternPosition(pattern);
            var candidateCycle = _patternBuffer.TakeLast(pos).ToList();

            if (candidateCycle.Count >= MinCycleLength && VerifyPatternSequenceRepeat(candidateCycle, pattern))
            {
                _cycleCount++;
                string validResponse = GetResponseBeforeLoop(pos);

                return new LoopDetectionResult
                {
                    IsLooping = true,
                    CycleCount = _cycleCount,
                    CycleLength = pos,
                    LastValidLine = _outputBuffer.LastOrDefault() ?? string.Empty,
                    Confidence = PatternMatchConfidence,
                    ValidResponse = validResponse
                };
            }
        }

        // --- Strategy 3: abnormally high output rate ---
        if (linesPerSecond > HighOutputRateThreshold && _outputBuffer.Count > HighOutputRateThreshold)
        {
            return new LoopDetectionResult
            {
                IsLooping = true,
                CycleCount = _cycleCount,
                CycleLength = 0,
                LastValidLine = _outputBuffer.LastOrDefault() ?? string.Empty,
                Confidence = HighOutputRateConfidence,
                ValidResponse = string.Join(Environment.NewLine, _outputBuffer)
            };
        }

        // No loop detected – record the line and return a clean result
        EnqueueLine(newLine, pattern);
        return new LoopDetectionResult { IsLooping = false };
    }

    /// <summary>
    /// Returns the content captured <em>before</em> the last
    /// <paramref name="cycleLength"/> lines (which form the repeating part).
    /// </summary>
    public string GetResponseBeforeLoop(int cycleLength)
    {
        int validCount = Math.Max(0, _outputBuffer.Count - cycleLength);
        var validLines = _outputBuffer.Take(validCount);
        return string.Join(Environment.NewLine, validLines);
    }

    /// <summary>Resets all state so the detector can be reused.</summary>
    public void Reset()
    {
        _outputBuffer.Clear();
        _patternBuffer.Clear();
        _recentLineTimes.Clear();
        _cycleCount = 0;
        _lastLineTime = DateTime.UtcNow;
    }

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Maintains a 1-second sliding window of line arrival times and returns
    /// how many lines arrived in that window (a stable lines-per-second
    /// estimate).
    /// </summary>
    private int UpdateOutputRate()
    {
        var now = DateTime.UtcNow;
        _recentLineTimes.Enqueue(now);

        // Evict entries older than 1 second
        var cutoff = now.AddSeconds(-1);
        while (_recentLineTimes.Count > 0 && _recentLineTimes.Peek() < cutoff)
            _recentLineTimes.Dequeue();

        _lastLineTime = now;
        return _recentLineTimes.Count; // lines in the last second
    }

    private void EnqueueLine(string line, string pattern)
    {
        _outputBuffer.Enqueue(line);
        _patternBuffer.Enqueue(pattern);

        if (_outputBuffer.Count > MaxBufferSize)
        {
            _outputBuffer.Dequeue();
            _patternBuffer.Dequeue();
        }
    }

    private int GetLinePosition(string line)
    {
        var reversed = _outputBuffer.Reverse().ToList();
        var idx = reversed.FindIndex(l => l == line);
        return idx >= 0 ? idx + 1 : 0;
    }

    private int GetPatternPosition(string pattern)
    {
        var reversed = _patternBuffer.Reverse().ToList();
        var idx = reversed.FindIndex(p => p == pattern);
        return idx >= 0 ? idx + 1 : 0;
    }

    /// <summary>
    /// Verifies that <paramref name="sequence"/> (the last N lines) forms a
    /// repeating cycle and that <paramref name="newLine"/> would be the first
    /// element of the next repetition.
    /// </summary>
    private static bool VerifySequenceRepeat(List<string> sequence, string newLine)
    {
        int n = sequence.Count;

        for (int cycleLen = MinCycleLength; cycleLen <= n / 2; cycleLen++)
        {
            if (n % cycleLen != 0)
                continue;

            bool isPattern = true;
            for (int i = 0; i < n; i++)
            {
                if (sequence[i] != sequence[i % cycleLen])
                {
                    isPattern = false;
                    break;
                }
            }

            if (isPattern && newLine == sequence[0])
                return true;
        }

        // Partial cycle: at least MinCycleLength lines have been seen and
        // the new line is restarting the sequence from the beginning.
        return newLine == sequence[0] && n >= MinCycleLength;
    }

    private static bool VerifyPatternSequenceRepeat(List<string> patterns, string newPattern)
    {
        int n = patterns.Count;

        for (int cycleLen = MinCycleLength; cycleLen <= n / 2; cycleLen++)
        {
            if (n % cycleLen != 0)
                continue;

            bool isPattern = true;
            for (int i = 0; i < n; i++)
            {
                if (patterns[i] != patterns[i % cycleLen])
                {
                    isPattern = false;
                    break;
                }
            }

            if (isPattern && newPattern == patterns[0])
                return true;
        }

        return newPattern == patterns[0] && n >= MinCycleLength;
    }

    /// <summary>
    /// Normalises a line by replacing variable tokens (numbers, e-mails,
    /// timestamps, UUIDs, file paths) with fixed placeholders so that
    /// structurally identical lines with different values are treated as the
    /// same pattern.
    /// </summary>
    public static string ExtractPattern(string line)
    {
        // UUIDs first (before generic number replacement)
        line = Regex.Replace(
            line,
            @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
            "[UUID]");

        // Timestamps  HH:MM:SS or HH:MM
        line = Regex.Replace(line, @"\d{2}:\d{2}(:\d{2})?", "[TIME]");

        // E-mail addresses
        line = Regex.Replace(
            line,
            @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
            "[EMAIL]");

        // Windows file paths  C:\foo\bar
        line = Regex.Replace(line, @"[A-Za-z]:\\[^\s""]*", "[PATH]");

        // Unix file paths  /foo/bar
        line = Regex.Replace(line, @"/[^\s""'*?<>|]+", "[PATH]");

        // Remaining numbers
        line = Regex.Replace(line, @"\d+", "[NUM]");

        return line;
    }
}
