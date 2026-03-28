# Loop Detection – Technical Reference

## Overview

When the GitHub Copilot CLI enters an infinite-output loop (continuous scroll),
`SmartLoopDetector` identifies the repetition and signals `CliExecutor` to stop
collecting output.  Only the valid portion of the response (captured *before*
the loop started) is returned to the caller and persisted to the log.

---

## Architecture

```
CLI process stdout/stderr
          │
          ▼
    CliExecutor.RunAsync()
          │  (per line)
          ▼
   SmartLoopDetector.DetectLoop()
          │
          ├─ IsLooping = false ──► append line, continue
          │
          └─ IsLooping = true  ──► stop capture
                                   extract ValidResponse
                                   alert user
                                   return ValidResponse to log
```

---

## Detection Strategies

### 1. Exact-match cycle detection (Confidence ≥ 0.95)

The new line is searched in the in-memory buffer.  If found, the subsequence
from its first occurrence to the current position is analysed.  If that
subsequence itself forms a repeating cycle *and* the new line would restart
it, the loop is confirmed.

**Example:**

```
Buffer:   [A, B, C]
New line: A           → matches buffer[0] → cycle [A,B,C] confirmed → LOOP
```

### 2. Pattern-match cycle detection (Confidence ≥ 0.80)

Variable tokens (numbers, e-mails, timestamps, UUIDs, file paths) are
normalised with placeholders before comparison, so structurally identical lines
with different values are treated as repetitions of the same pattern.

| Token | Placeholder |
|-------|-------------|
| UUID  | `[UUID]`    |
| `HH:MM` / `HH:MM:SS` | `[TIME]` |
| e-mail address | `[EMAIL]` |
| Windows path `C:\…` | `[PATH]`  |
| Unix path `/…` | `[PATH]`  |
| Any remaining integer | `[NUM]` |

**Example:**

```
Line 1:   "Processing item 1..."  →  pattern "Processing item [NUM]..."
Line 2:   "Checking status 200"   →  pattern "Checking status [NUM]"
Line 3:   "Processing item 2..."  →  pattern "Processing item [NUM]..."
Line 4:   "Checking status 404"   →  pattern "Checking status [NUM]"
New line: "Processing item 3..."  →  pattern seen before → LOOP
```

### 3. High output-rate detection (Confidence ≥ 0.70)

If the CLI emits more than 50 lines per second *and* the buffer already holds
more than 50 lines, the output rate is deemed pathological and a loop is
flagged.  This catches loops that produce entirely unique lines (e.g. an
incrementing counter) but at an abnormal speed.

---

## Data Model – `LoopDetectionResult`

| Property | Type | Description |
|----------|------|-------------|
| `IsLooping` | `bool` | `true` when a loop was detected |
| `CycleCount` | `int` | How many times the cycle has repeated |
| `CycleLength` | `int` | Number of lines in one cycle |
| `LastValidLine` | `string` | Last line before looping began |
| `Confidence` | `double` | Detection confidence (0–1) |
| `ValidResponse` | `string` | Output captured before the loop |

---

## CliExecutor Integration

```csharp
var result = _loopDetector.DetectLoop(line);

if (result.IsLooping)
{
    // Stop collecting; return only the valid portion
    validResponseOnLoop = result.ValidResponse;
    Console.WriteLine($"⚠️  Loop detectado após {result.CycleCount} ciclo(s).");
    Console.WriteLine("✅ Resposta válida capturada e salva no log.");
    Console.WriteLine("⌨️  Pressione Ctrl+C para interromper o CLI se necessário.");
    loopCts.Cancel();
    return;
}
```

The `CliExecutor` creates a fresh `SmartLoopDetector` for every `RunAsync`
call so state never leaks between invocations.

---

## Memory usage

The detector keeps at most **1 000 lines** in its circular buffer (both the raw
and the normalised-pattern buffer).  For very long responses the oldest lines
are evicted automatically, keeping memory bounded.

---

## Limitations

* Detection requires **at least two** lines in a cycle (`MinCycleLength = 2`).
  A loop that alternates between a single repeated line is detected as soon as
  the same line appears a second time.
* If the valid response itself contains a repeated pattern (e.g. two identical
  log lines for legitimate reasons), the first repetition will be flagged.
  Adjust `MinCycleLength` in `SmartLoopDetector` if this causes false
  positives.
