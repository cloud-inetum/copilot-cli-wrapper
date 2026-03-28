# Loop Detection V2 – Technical Documentation

## Overview

Version 2.0.0 introduces **SmartLoopDetector**, a reusable component that identifies
infinite-loop behaviour in streaming CLI output and extracts only the valid (pre-loop)
portion of the response.

---

## How It Works

`SmartLoopDetector` maintains an in-memory ring buffer (up to **1 000 lines**) and
analyses each new line as it arrives.  Three complementary strategies are applied in
priority order:

### Strategy 1 – Exact Match

Each incoming line is compared against the buffer.  When a line that already exists in
the buffer is found, the algorithm calculates the candidate **cycle length**
(`buffer.Count − position_of_first_occurrence`).  A second occurrence of the same
sequence *before* that position is required to confirm the cycle.

```
Buffer before line 7:
  [0] Starting analysis...     ← first cycle
  [1] Checking dependencies...
  [2] Compiling code...
  [3] Starting analysis...     ← second cycle
  [4] Checking dependencies...
  [5] Compiling code...

Incoming: "Starting analysis..."
  firstOccurrence = 3, cycleLen = 3
  verifyStart = 0
  [0..2] == [3..5]  ✓  →  loop detected!
```

### Strategy 2 – Pattern Match

Each line is normalised before the same cycle-length check is applied.  Normalisation
replaces variable tokens with fixed placeholders so that lines differing only in data
values compare equal:

| Token type | Regex | Placeholder |
|---|---|---|
| UUID | `[0-9a-f]{8}-…` | `[UUID]` |
| E-mail address | `[a-z]+@[a-z]+\.[a-z]+` | `[EMAIL]` |
| Timestamp | `\d{1,2}:\d{2}(:\d{2})?` | `[TIME]` |
| File path | `/path/to/file` or `C:\path` | `[PATH]` |
| Number | `\d+` | `[NUM]` |

**Example:**

```
"Loading module 1 at 09:00:01"  →  "Loading module [NUM] at [TIME]"
"Validating module 1 at 09:00:02"  →  "Validating module [NUM] at [TIME]"
```

After normalisation these two patterns form a detectable 2-element cycle even though
no exact string was repeated.

### Strategy 3 – High-Speed Output

When the output rate exceeds **50 lines/second** (measured over a sliding window of the
last 50 timestamps) the detector flags the output as suspicious.  A minimum of
**30 buffered lines** is required before the speed check runs, preventing false
positives on short inputs.

The confidence score for high-speed detection is:

```
confidence = min(0.5 + (lps − 50) / 200, 0.9)
```

---

## Confidence Scoring

| Detection strategy | Confidence |
|---|---|
| Exact cycle confirmed | 0.95 |
| Pattern cycle confirmed | 0.85 |
| High-speed only | 0.50 – 0.90 |

---

## LoopDetectionResult

```csharp
public class LoopDetectionResult
{
    bool   IsLooping          // true when a loop is detected
    int    CycleCount         // number of complete cycles detected so far
    int    CycleLength        // length of the repeating cycle in lines
    string LastValidLine      // last line captured before looping started
    double Confidence         // 0 – 1 detection confidence
    string ValidResponse      // accumulated output before the loop
    string RepeatingPattern   // normalised or literal repeating pattern
    int    LoopStartPosition  // zero-based index in buffer where looping began
}
```

---

## Integration with CliExecutor

`CliExecutor.RunAsync()` now returns a `CliExecutionResult` instead of a plain string:

```csharp
public class CliExecutionResult
{
    string              Output         // valid (pre-loop) CLI output
    LoopDetectionResult? LoopDetection // null when no loop was detected
    bool                LoopDetected   // convenience flag
}
```

When a loop is detected the CLI process is killed immediately, the valid portion of the
response is saved to the log, and the user is notified:

```
[⚠️  Loop infinito detectado!]
[✅ Ciclos detectados: 3]
[✅ Tamanho do ciclo: 12 linhas]
[✅ Confiança: 95%]
[✅ Resposta válida salva no log]
[⌨️  Pressione Ctrl+C para parar o CLI]
```

---

## Enhanced LogManager

`LogManager.WriteEntry()` accepts an optional `loopDetectedAt` timestamp.  When
provided the log entry is annotated with a detection notice:

```markdown
> ⚠️ **loop_detected: true** | Detected at: 14:32:05.123
```

---

## Use Cases Covered

| Scenario | Detection strategy |
|---|---|
| Exact A–B–C–A–B–C–A repetition | Exact |
| Loop with variable counters (`item 1`, `item 2`, …) | Exact *or* Pattern |
| Loop with varying timestamps | Pattern |
| Mixed variation (numbers + timestamps) | Pattern |
| Runaway output > 50 lines/s | High-speed |
| Multiple cycles | Exact (cycle count incremented) |

---

## Known Limitations

* **Minimum 4 buffered lines** before cycle detection can run (two full cycles
  of the minimum length 2 are needed).
* **Minimum 30 buffered lines** before the high-speed check runs, meaning very
  short bursts of output cannot be flagged by speed alone.
* Pattern normalisation is English/ASCII-centric.  Unicode-heavy output with numeric
  characters outside `0–9` is not normalised.
* The detector is stateful – each `CliExecutor.RunAsync()` call creates a fresh
  `SmartLoopDetector` instance.

---

## Example Output

```bash
$ dotnet run
> copilot explain ./error.log

[Capturando resposta...]

A função processItem() em linha 45 lança uma exceção...
O erro ocorre quando a lista é vazia...

[⚠️  Loop infinito detectado!]
[✅ Ciclos detectados: 3]
[✅ Tamanho do ciclo: 12 linhas]
[✅ Confiança: 95%]
[✅ Resposta válida salva no log]
[⌨️  Pressione Ctrl+C para parar o CLI]

>
```
