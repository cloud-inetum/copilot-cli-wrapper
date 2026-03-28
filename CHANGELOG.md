# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.0.0] – 2026-03-28

### Added

- **`SmartLoopDetector`** – reusable component that detects infinite loops in
  streaming CLI output using three complementary strategies:
  - *Exact-match cycle detection* – confirms a cycle when a repeated line
    sequence is verified against the buffer history.
  - *Pattern-match cycle detection* – normalises each line (replacing numbers,
    timestamps, e-mails, UUIDs, and file paths with placeholders) before applying
    the same cycle-length check, catching loops where content varies on each
    iteration.
  - *High-speed detection* – flags output arriving at more than 50 lines/second
    (requires a minimum of 30 buffered lines for a reliable measurement).
- **`LoopDetectionResult`** – value object returned by `SmartLoopDetector.Analyse()`
  with full loop metadata:
  - `IsLooping`, `CycleCount`, `CycleLength`, `Confidence`
  - `ValidResponse` – captured output before the loop began
  - `RepeatingPattern`, `LoopStartPosition`, `LastValidLine`
- **`CliExecutionResult`** – replaces the plain `string` return type of
  `CliExecutor.RunAsync()`.  Contains the valid output and an optional
  `LoopDetectionResult`.
- **`CliExecutor`** now integrates `SmartLoopDetector` per execution:
  - Stops the CLI process immediately when a loop is detected.
  - Returns only the valid (pre-loop) portion of the response.
  - Prints a user-facing warning with cycle metadata.
- **`LogManager.WriteEntry()`** accepts an optional `loopDetectedAt` timestamp and
  annotates log entries with `loop_detected: true` when applicable.
- **`SmartLoopDetectorTests`** – 19 unit tests covering all detection scenarios:
  exact loops, variable-number loops, timestamp loops, mixed-variation loops,
  high-speed detection, multiple cycles, and no-loop cases.
- **`docs/LOOP_DETECTION_V2.md`** – comprehensive technical documentation for the
  loop-detection subsystem.

### Changed

- `CliExecutor.RunAsync()` return type changed from `Task<string>` to
  `Task<CliExecutionResult>`.  Callers receive both the output and any loop metadata.
- `LogManager.WriteEntry()` gains an optional `DateTime? loopDetectedAt` parameter
  (backward-compatible – existing call-sites pass no argument and behaviour is
  unchanged).
- `CopilotCliWrapper.ExecuteAndLogAsync()` updated to consume `CliExecutionResult`
  and forward loop-detection metadata to the log.

### Breaking Changes

- **`CliExecutor.RunAsync()`** now returns `Task<CliExecutionResult>` instead of
  `Task<string>`.  Any code that calls this method directly (outside the wrapper)
  must be updated to read the `.Output` property.

---

## [1.0.0] – 2026-03-28

### Added

- Interactive REPL mode fully compatible with GitHub Copilot CLI.
- Automatic Markdown logging of every question/answer pair.
- In-memory conversation history with `history`, `search`, `export`, and `clear`
  commands.
- Export in JSON, CSV, and Markdown formats.
- Multi-platform support (Windows, macOS, Linux).
- Model selection via `model <name>` command.
- Complete documentation (`README.md`, `docs/USAGE.md`, `docs/ARCHITECTURE.md`,
  `docs/API.md`).
- Unit tests for `CommandParser`, `ConversationEntry`, `LogManager`, and
  `CopilotCliWrapper` search functionality.
