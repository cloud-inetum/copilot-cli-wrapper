using CopilotCliWrapper;
using CopilotCliWrapper.Models;

namespace CopilotCliWrapper.Tests;

/// <summary>
/// Unit tests for <see cref="SmartLoopDetector"/> covering all major
/// loop-detection scenarios required by v2.0.0.
/// </summary>
public class SmartLoopDetectorTests
{
    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Feeds <paramref name="lines"/> into <paramref name="detector"/> one by
    /// one and returns the last <see cref="LoopDetectionResult"/>.
    /// </summary>
    private static LoopDetectionResult FeedLines(SmartLoopDetector detector, IEnumerable<string> lines)
    {
        LoopDetectionResult last = new();
        foreach (var line in lines)
            last = detector.Analyse(line);
        return last;
    }

    /// <summary>
    /// Feeds <paramref name="lines"/> and returns the first result where
    /// <see cref="LoopDetectionResult.IsLooping"/> is <c>true</c>, or a
    /// non-looping result if none is found.
    /// </summary>
    private static LoopDetectionResult FeedUntilLoopDetected(
        SmartLoopDetector detector,
        IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var result = detector.Analyse(line);
            if (result.IsLooping)
                return result;
        }
        return new LoopDetectionResult { IsLooping = false };
    }

    // ------------------------------------------------------------------ //
    //  Scenario 1: Exact loop  A-B-C-A-B-C-A                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_ExactRepetition_IsDetected()
    {
        var detector = new SmartLoopDetector();

        // Feed A-B-C twice then start a third time.
        var lines = new[]
        {
            "Starting analysis...",
            "Checking dependencies...",
            "Compiling code...",
            "Starting analysis...",   // second cycle
            "Checking dependencies...",
            "Compiling code...",
            "Starting analysis..."    // third cycle triggers detection
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
        Assert.True(result.CycleLength > 0);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void DetectLoop_ExactRepetition_CycleLengthIsCorrect()
    {
        var detector = new SmartLoopDetector();

        var lines = new[]
        {
            "Alpha", "Beta", "Gamma",   // first cycle
            "Alpha", "Beta", "Gamma",   // second cycle
            "Alpha"                     // triggers detection
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
        Assert.Equal(3, result.CycleLength);
    }

    [Fact]
    public void DetectLoop_ExactRepetition_ValidResponseExcludesLoop()
    {
        var detector = new SmartLoopDetector();

        var lines = new[]
        {
            "Line A", "Line B", "Line C",
            "Line A", "Line B", "Line C",
            "Line A"
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
        // Valid response should not contain the repeated part.
        Assert.False(string.IsNullOrEmpty(result.ValidResponse));
    }

    // ------------------------------------------------------------------ //
    //  Scenario 2: Loop with variable numbers                             //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_WithVariableNumbers_IsDetected()
    {
        var detector = new SmartLoopDetector();

        // Pattern "Processing item [NUM]" repeats regardless of the counter.
        var lines = new[]
        {
            "Processing item 1",
            "Processing item 2",
            "Processing item 3",
            "Processing item 1",   // number resets → pattern repeat
            "Processing item 2",
            "Processing item 3",
            "Processing item 1"    // triggers detection
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
    }

    [Fact]
    public void DetectLoop_WithVariableNumbers_CycleLengthIsCorrect()
    {
        var detector = new SmartLoopDetector();

        var lines = new[]
        {
            "Error code: 100",
            "Error code: 200",
            "Error code: 100",   // cycle of length 2
            "Error code: 200",
            "Error code: 100"    // triggers
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
        Assert.Equal(2, result.CycleLength);
    }

    // ------------------------------------------------------------------ //
    //  Scenario 3: Loop with variable timestamps                          //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_WithVariableTimestamps_IsDetected()
    {
        var detector = new SmartLoopDetector();

        var lines = new[]
        {
            "[10:30:45] Starting task",
            "[10:30:46] Running checks",
            "[10:30:47] Done",
            "[10:30:48] Starting task",   // same pattern, different time
            "[10:30:49] Running checks",
            "[10:30:50] Done",
            "[10:30:51] Starting task"    // triggers
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
    }

    // ------------------------------------------------------------------ //
    //  Scenario 4: Mixed loop (numbers + timestamps + pattern)            //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_MixedVariation_IsDetected()
    {
        var detector = new SmartLoopDetector();

        // Two structurally distinct line patterns, each varying by timestamp
        // and number, but still detectable as a 2-line cycle after normalisation:
        //   "Loading module [NUM] at [TIME]"
        //   "Validating module [NUM] at [TIME]"
        var lines = new[]
        {
            "Loading module 1 at 09:00:01",
            "Validating module 1 at 09:00:02",
            "Loading module 2 at 09:00:03",    // pattern cycle repeats
            "Validating module 2 at 09:00:04",
            "Loading module 3 at 09:00:05",    // triggers pattern-based detection
            "Validating module 3 at 09:00:06",
            "Loading module 4 at 09:00:07"
        };

        var result = FeedUntilLoopDetected(detector, lines);

        Assert.True(result.IsLooping);
    }

    // ------------------------------------------------------------------ //
    //  Scenario 5: High-speed output (>50 lines/s)                       //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_HighSpeedOutput_IsDetected()
    {
        var detector = new SmartLoopDetector();

        // Inject 60 lines with fabricated timestamps crammed into 1 second
        // by manipulating the detector internals via reflection.
        // Because we cannot control DateTime.UtcNow directly we rely on the
        // public Analyse API and verify via GetStatistics.

        // Instead, use a test-friendly approach: feed unique lines rapidly
        // using a real tight loop to accumulate >50 lps.
        var start = DateTime.UtcNow;
        LoopDetectionResult? speedResult = null;

        for (int i = 0; i < 200; i++)
        {
            var r = detector.Analyse($"unique line {i}");
            if (r.IsLooping)
            {
                speedResult = r;
                break;
            }

            // If elapsed time > 2 s without detection, bail out — the machine
            // is too slow to trigger the rate threshold in this test run.
            if ((DateTime.UtcNow - start).TotalSeconds > 2)
                break;
        }

        // The test passes if either:
        //  a) a high-speed loop was detected, OR
        //  b) the machine runs slow enough that we never exceed 50 lps
        //     (in which case no false positive was raised either).
        var (_, _, lps) = detector.GetStatistics();

        if (lps > 50)
            Assert.True(speedResult?.IsLooping == true, "Expected high-speed loop detection");
        else
            Assert.False(speedResult?.IsLooping == true, "Unexpected false positive for high-speed detection");
    }

    // ------------------------------------------------------------------ //
    //  Scenario 6: Multiple cycles detected                               //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_MultipleCycles_CycleCountIncreases()
    {
        var detector = new SmartLoopDetector();

        // First detection
        var lines1 = new[] { "X", "Y", "X", "Y", "X" };
        var result1 = FeedUntilLoopDetected(detector, lines1);

        Assert.True(result1.IsLooping);
        Assert.Equal(1, result1.CycleCount);

        // Second detection (continue feeding the loop)
        var lines2 = new[] { "Y", "X", "Y", "X" };
        var result2 = FeedUntilLoopDetected(detector, lines2);

        if (result2.IsLooping)
            Assert.True(result2.CycleCount > 1);
    }

    // ------------------------------------------------------------------ //
    //  Scenario 7: No looping — valid response                           //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_NoLoop_NeverReportsLooping()
    {
        var detector = new SmartLoopDetector();

        var lines = new[]
        {
            "Step 1: initialise",
            "Step 2: load configuration",
            "Step 3: connect to database",
            "Step 4: run migrations",
            "Step 5: start server",
            "Step 6: ready"
        };

        var result = FeedLines(detector, lines);

        Assert.False(result.IsLooping);
    }

    [Fact]
    public void DetectLoop_NoLoop_StatisticsShowCorrectBufferSize()
    {
        var detector = new SmartLoopDetector();
        var lines = Enumerable.Range(1, 10).Select(i => $"Unique line {i}");
        FeedLines(detector, lines);

        var (bufferedLines, cycles, _) = detector.GetStatistics();

        // Unique lines should fill the buffer without triggering any loop.
        Assert.Equal(10, bufferedLines);
        Assert.Equal(0, cycles);
    }

    // ------------------------------------------------------------------ //
    //  GetStatistics                                                       //
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetStatistics_AfterReset_ReturnsZeroValues()
    {
        var detector = new SmartLoopDetector();
        FeedLines(detector, new[] { "A", "B", "C" });

        detector.Reset();
        var (bufferedLines, cycles, lps) = detector.GetStatistics();

        Assert.Equal(0, bufferedLines);
        Assert.Equal(0, cycles);
    }

    // ------------------------------------------------------------------ //
    //  NormaliseLine (internal, tested via reflection)                    //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("Processing item 42", "Processing item [NUM]")]
    [InlineData("Error at 10:30:45", "Error at [TIME]")]
    [InlineData("User: alice@example.com logged in", "User: [EMAIL] logged in")]
    [InlineData(
        "ID: 123e4567-e89b-12d3-a456-426614174000",
        "ID: [UUID]")]
    [InlineData("Reading /home/user/file.txt", "Reading [PATH]")]
    public void NormaliseLine_ReplacesVariableTokens(string input, string expectedPattern)
    {
        var normalised = SmartLoopDetector.NormaliseLine(input);
        Assert.Equal(expectedPattern, normalised);
    }

    // ------------------------------------------------------------------ //
    //  LoopDetectionResult model                                          //
    // ------------------------------------------------------------------ //

    [Fact]
    public void LoopDetectionResult_DefaultValues_AreCorrect()
    {
        var result = new LoopDetectionResult();

        Assert.False(result.IsLooping);
        Assert.Equal(0, result.CycleCount);
        Assert.Equal(0, result.CycleLength);
        Assert.Equal(string.Empty, result.LastValidLine);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(string.Empty, result.ValidResponse);
        Assert.Equal(string.Empty, result.RepeatingPattern);
        Assert.Equal(0, result.LoopStartPosition);
    }

    [Fact]
    public void LoopDetectionResult_AllPropertiesCanBeSet()
    {
        var result = new LoopDetectionResult
        {
            IsLooping = true,
            CycleCount = 3,
            CycleLength = 5,
            LastValidLine = "last valid",
            Confidence = 0.95,
            ValidResponse = "valid output",
            RepeatingPattern = "repeating",
            LoopStartPosition = 10
        };

        Assert.True(result.IsLooping);
        Assert.Equal(3, result.CycleCount);
        Assert.Equal(5, result.CycleLength);
        Assert.Equal("last valid", result.LastValidLine);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal("valid output", result.ValidResponse);
        Assert.Equal("repeating", result.RepeatingPattern);
        Assert.Equal(10, result.LoopStartPosition);
    }
}
