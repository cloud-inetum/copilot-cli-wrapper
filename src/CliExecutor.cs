using System.Diagnostics;
using System.Runtime.InteropServices;
using CopilotCliWrapper.Models;

namespace CopilotCliWrapper;

/// <summary>
/// Locates and executes the native GitHub Copilot CLI, streaming its
/// stdout/stderr back to the caller while capturing the combined output.
/// </summary>
public class CliExecutor
{
    private readonly string _cliPath;

    public CliExecutor(string? cliPath = null)
    {
        _cliPath = cliPath ?? DetectCliPath();
    }

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Executes the Copilot CLI with <paramref name="arguments"/>, streaming
    /// output to the console and returning the captured combined output.
    /// When an infinite loop is detected the process is stopped early and
    /// only the valid (pre-loop) portion of the output is returned.
    /// </summary>
    public async Task<CliExecutionResult> RunAsync(string arguments, CancellationToken cancellationToken = default)
    {
        ValidateCli();

        var detector = new SmartLoopDetector();
        var psi = BuildProcessStartInfo(arguments);
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var outputBuilder = new System.Text.StringBuilder();
        var tcs = new TaskCompletionSource<int>();
        LoopDetectionResult? loopResult = null;
        var loopDetected = false;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (loopDetected) return;

            var detection = detector.Analyse(e.Data);

            if (detection.IsLooping)
            {
                loopDetected = true;
                loopResult = detection;

                PrintLoopWarning(detection);

                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return;
            }

            Console.WriteLine(e.Data);
            outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            if (loopDetected) return;
            Console.Error.WriteLine(e.Data);
            outputBuilder.AppendLine(e.Data);
        };

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = cancellationToken.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
        });

        await tcs.Task;

        if (loopDetected && loopResult is not null)
        {
            // Return only the valid (pre-loop) response.
            var validOutput = string.IsNullOrEmpty(loopResult.ValidResponse)
                ? outputBuilder.ToString()
                : loopResult.ValidResponse;

            return new CliExecutionResult(validOutput, loopResult);
        }

        return new CliExecutionResult(outputBuilder.ToString(), null);
    }

    /// <summary>
    /// Returns the resolved path to the Copilot CLI binary.
    /// </summary>
    public string CliPath => _cliPath;

    /// <summary>
    /// Returns <c>true</c> when the Copilot CLI is found on the current system.
    /// </summary>
    public bool IsInstalled() => File.Exists(_cliPath) || IsOnPath("copilot");

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    private ProcessStartInfo BuildProcessStartInfo(string arguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{_cliPath}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false
            };
        }

        // macOS / Linux — honour $SHELL or fall back to bash
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
        return new ProcessStartInfo
        {
            FileName = shell,
            Arguments = $"-c '\"{_cliPath}\" {arguments}'",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false
        };
    }

    private void ValidateCli()
    {
        if (!File.Exists(_cliPath) && !IsOnPath("copilot"))
        {
            throw new InvalidOperationException(
                $"GitHub Copilot CLI not found at '{_cliPath}'. " +
                "Please install it with: npm install -g @github/copilot-cli");
        }
    }

    private static string DetectCliPath()
    {
        // 1. Honour explicit environment variable
        var envPath = Environment.GetEnvironmentVariable("COPILOT_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        // 2. Search well-known locations
        var candidates = GetCandidatePaths();
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // 3. Fall back to plain name and let the OS resolve it via PATH
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot.cmd" : "copilot";
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            yield return Path.Combine(appData, "npm", "copilot.cmd");
            yield return @"C:\Program Files\GitHub CLI\copilot.cmd";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/usr/local/bin/copilot";
            yield return "/opt/homebrew/bin/copilot";
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".npm-global", "bin", "copilot");
        }
        else
        {
            yield return "/usr/bin/copilot";
            yield return "/usr/local/bin/copilot";
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".npm-global", "bin", "copilot");
        }
    }

    private static bool IsOnPath(string executable)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
                    ?? Array.Empty<string>();

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".cmd", ".exe", ".bat", "" }
            : new[] { "" };

        return paths.Any(dir =>
            extensions.Any(ext => File.Exists(Path.Combine(dir, executable + ext))));
    }

    private static void PrintLoopWarning(LoopDetectionResult result)
    {
        Console.WriteLine();
        Console.WriteLine("[⚠️  Loop infinito detectado!]");
        Console.WriteLine($"[✅ Ciclos detectados: {result.CycleCount}]");
        Console.WriteLine($"[✅ Tamanho do ciclo: {result.CycleLength} linhas]");
        Console.WriteLine($"[✅ Confiança: {result.Confidence * 100:F0}%]");
        Console.WriteLine("[✅ Resposta válida salva no log]");
        Console.WriteLine("[⌨️  Pressione Ctrl+C para parar o CLI]");
    }
}

/// <summary>
/// Encapsulates the result of a CLI execution, including any loop-detection
/// metadata.
/// </summary>
public sealed class CliExecutionResult
{
    /// <summary>
    /// The captured (valid, pre-loop) output from the CLI process.
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Loop-detection metadata when a loop was detected; <c>null</c> otherwise.
    /// </summary>
    public LoopDetectionResult? LoopDetection { get; }

    /// <summary>Whether a loop was detected during this execution.</summary>
    public bool LoopDetected => LoopDetection?.IsLooping == true;

    public CliExecutionResult(string output, LoopDetectionResult? loopDetection)
    {
        Output = output;
        LoopDetection = loopDetection;
    }
}
