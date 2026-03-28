# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.0.0] – 2026-03-28

### Added

- **`SmartLoopDetector`** – intelligent infinite-loop detection using an
  in-memory history buffer (up to 1 000 lines):
  - Exact-match cycle detection (confidence 0.95): detects when the same
    sequence of lines starts repeating.
  - Pattern-match cycle detection (confidence 0.80): detects cycles where
    lines vary only in numbers, e-mails, timestamps, UUIDs, or file paths.
  - High-output-rate detection (confidence 0.70): flags pathological output
    velocity (> 50 lines/second).
- **`LoopDetectionResult`** – result model carrying `IsLooping`, `CycleCount`,
  `CycleLength`, `LastValidLine`, `Confidence`, and `ValidResponse`.
- **`SmartLoopDetectorTests`** – 18 test cases covering exact loops, loops with
  variable data, pattern extraction, `GetResponseBeforeLoop`, and `Reset`.
- **`docs/LOOP_DETECTION.md`** – full technical reference for the loop
  detection subsystem.

### Changed

- **`CliExecutor.RunAsync`** – integrates `SmartLoopDetector` on every
  invocation.  When a loop is detected:
  - Output collection stops immediately.
  - Only the valid response (captured before the loop) is returned.
  - A warning is printed to the console with cycle count and confidence.
  - The user retains control (Ctrl+C to kill the CLI process).
- **`docs/USAGE.md`** – new section documenting loop-detection behaviour and
  console output.

---

## [1.0.0] – 2026-03-28

### Added

- Interactive REPL mode forwarding prompts to the native GitHub Copilot CLI.
- Automatic logging of every Q&A pair to a timestamped Markdown file.
- In-memory conversation history with `history`, `search`, `clear` commands.
- Multi-format export: JSON, CSV, Markdown (`export <format>`).
- Model switching mid-session (`model <name>`).
- Cross-platform support: Windows, macOS, Linux.
- `CommandParser`, `LogManager`, `ConversationEntry`, `SessionInfo` classes.
- Comprehensive unit-test suite (34 tests).
- Documentation: `README.md`, `USAGE.md`, `API.md`, `ARCHITECTURE.md`.
