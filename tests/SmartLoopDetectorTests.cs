using CopilotCliWrapper;

namespace CopilotCliWrapper.Tests;

/// <summary>
/// Tests for <see cref="SmartLoopDetector"/> and <see cref="LoopDetectionResult"/>.
/// </summary>
public class SmartLoopDetectorTests
{
    // ------------------------------------------------------------------ //
    //  Exact-match loop detection                                          //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_NoLoop_ReturnsFalse()
    {
        var detector = new SmartLoopDetector();

        Assert.False(detector.DetectLoop("Line A").IsLooping);
        Assert.False(detector.DetectLoop("Line B").IsLooping);
        Assert.False(detector.DetectLoop("Line C").IsLooping);
    }

    [Fact]
    public void DetectLoop_ExactLoop_DetectedOnSecondCycle()
    {
        var detector = new SmartLoopDetector();

        // First cycle – lines are recorded
        detector.DetectLoop("Starting analysis...");
        detector.DetectLoop("Checking dependencies...");
        detector.DetectLoop("Compiling code...");

        // Second cycle begins – should detect the loop
        var result = detector.DetectLoop("Starting analysis...");

        Assert.True(result.IsLooping);
        Assert.True(result.CycleCount >= 1);
        Assert.Equal(3, result.CycleLength);
        Assert.True(result.Confidence >= 0.90);
    }

    [Fact]
    public void DetectLoop_ExactLoop_ValidResponseExcludesLoopLines()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("Valid line 1");
        detector.DetectLoop("Valid line 2");
        detector.DetectLoop("A");
        detector.DetectLoop("B");
        detector.DetectLoop("C");
        // Loop starts here:
        var result = detector.DetectLoop("A");

        Assert.True(result.IsLooping);
        Assert.Contains("Valid line 1", result.ValidResponse);
        Assert.Contains("Valid line 2", result.ValidResponse);
        // The repeating cycle (A/B/C) should not appear in the valid response
        Assert.DoesNotContain("A\nA", result.ValidResponse);
    }

    [Fact]
    public void DetectLoop_MultipleCycles_CountsCorrectly()
    {
        var detector = new SmartLoopDetector();

        // First cycle – lines are recorded in the buffer
        detector.DetectLoop("Ping");
        detector.DetectLoop("Pong");

        // Second cycle begins – the repeat is detected immediately
        var result = detector.DetectLoop("Ping");

        Assert.True(result.IsLooping);
        Assert.True(result.CycleCount >= 1);
        Assert.Equal(2, result.CycleLength);
    }

    [Fact]
    public void DetectLoop_SingleUniqueLines_DoesNotFalsePositive()
    {
        var detector = new SmartLoopDetector();

        // Lines that look similar but are all unique
        for (int i = 0; i < 20; i++)
            Assert.False(detector.DetectLoop($"Processing step {i}").IsLooping);
    }

    // ------------------------------------------------------------------ //
    //  Pattern-match loop detection (variable data in lines)              //
    // ------------------------------------------------------------------ //

    [Fact]
    public void DetectLoop_LoopWithVariableNumbers_DetectedByPattern()
    {
        var detector = new SmartLoopDetector();

        // First cycle with different numbers
        detector.DetectLoop("Processing item 1...");
        detector.DetectLoop("Checking status 200");
        detector.DetectLoop("Processing item 2...");
        detector.DetectLoop("Checking status 404");

        // Second cycle – numbers differ but pattern is the same
        var result = detector.DetectLoop("Processing item 3...");

        Assert.True(result.IsLooping);
        Assert.Equal(0.80, result.Confidence, precision: 2);
    }

    [Fact]
    public void DetectLoop_LoopWithVariableEmails_DetectedByPattern()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("Sending email to alice@example.com");
        detector.DetectLoop("Retry: alice@example.com");
        detector.DetectLoop("Sending email to bob@example.com");
        detector.DetectLoop("Retry: bob@example.com");

        // Third cycle starts
        var result = detector.DetectLoop("Sending email to carol@example.com");

        Assert.True(result.IsLooping);
    }

    [Fact]
    public void DetectLoop_LoopWithVariableTimestamps_DetectedByPattern()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("[10:30:01] Polling server...");
        detector.DetectLoop("[10:30:01] No response");
        detector.DetectLoop("[10:30:02] Polling server...");
        detector.DetectLoop("[10:30:02] No response");

        var result = detector.DetectLoop("[10:30:03] Polling server...");

        Assert.True(result.IsLooping);
    }

    [Fact]
    public void DetectLoop_LoopWithVariablePaths_DetectedByPattern()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("Reading /var/log/app1.log");
        detector.DetectLoop("Failed to parse /var/log/app1.log");
        detector.DetectLoop("Reading /var/log/app2.log");
        detector.DetectLoop("Failed to parse /var/log/app2.log");

        var result = detector.DetectLoop("Reading /var/log/app3.log");

        Assert.True(result.IsLooping);
    }

    // ------------------------------------------------------------------ //
    //  Pattern extraction                                                  //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("Processing item 42", "Processing item [NUM]")]
    [InlineData("Error at 10:30:45", "Error at [TIME]")]
    [InlineData("User: john@mail.com", "User: [EMAIL]")]
    [InlineData(
        "ID: 550e8400-e29b-41d4-a716-446655440000",
        "ID: [UUID]")]
    public void ExtractPattern_ReplacesVariableTokens(string input, string expectedPattern)
    {
        var result = SmartLoopDetector.ExtractPattern(input);
        Assert.Equal(expectedPattern, result);
    }

    [Fact]
    public void ExtractPattern_MixedTokens_ReplacesAll()
    {
        var input = "10:30:45 user@host.com processed 3 items at /var/log/out.txt";
        var pattern = SmartLoopDetector.ExtractPattern(input);

        Assert.Contains("[TIME]", pattern);
        Assert.Contains("[EMAIL]", pattern);
        Assert.Contains("[NUM]", pattern);
        Assert.Contains("[PATH]", pattern);
        Assert.DoesNotContain("user@host.com", pattern);
    }

    // ------------------------------------------------------------------ //
    //  GetResponseBeforeLoop                                               //
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetResponseBeforeLoop_ReturnsLinesBeforeCycle()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("Intro line");
        detector.DetectLoop("A");
        detector.DetectLoop("B");

        // Trigger detection
        var result = detector.DetectLoop("A");

        Assert.True(result.IsLooping);
        Assert.Contains("Intro line", result.ValidResponse);
    }

    [Fact]
    public void GetResponseBeforeLoop_WithZeroCycleLength_ReturnsFullBuffer()
    {
        var detector = new SmartLoopDetector();
        detector.DetectLoop("Line 1");
        detector.DetectLoop("Line 2");

        var response = detector.GetResponseBeforeLoop(0);

        Assert.Contains("Line 1", response);
        Assert.Contains("Line 2", response);
    }

    // ------------------------------------------------------------------ //
    //  Reset                                                               //
    // ------------------------------------------------------------------ //

    [Fact]
    public void Reset_ClearsStateAndAllowsReuse()
    {
        var detector = new SmartLoopDetector();

        detector.DetectLoop("A");
        detector.DetectLoop("B");
        detector.Reset();

        // After reset, the previously-seen lines should not trigger a loop
        Assert.False(detector.DetectLoop("A").IsLooping);
        Assert.False(detector.DetectLoop("B").IsLooping);
    }

    // ------------------------------------------------------------------ //
    //  LoopDetectionResult defaults                                        //
    // ------------------------------------------------------------------ //

    [Fact]
    public void LoopDetectionResult_DefaultsToNotLooping()
    {
        var result = new LoopDetectionResult();

        Assert.False(result.IsLooping);
        Assert.Equal(0, result.CycleCount);
        Assert.Equal(0, result.CycleLength);
        Assert.Equal(string.Empty, result.LastValidLine);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(string.Empty, result.ValidResponse);
    }

}
