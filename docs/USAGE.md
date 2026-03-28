# Usage Guide

## Installation

### Prerequisites

* .NET 8 SDK (or later)
* GitHub Copilot CLI installed and authenticated  
  ```bash
  npm install -g @github/copilot-cli
  github-copilot-cli auth
  ```

### Running the wrapper

```bash
# Clone and build
git clone https://github.com/cloud-inetum/copilot-cli-wrapper
cd copilot-cli-wrapper

dotnet build CopilotCliWrapper.slnx
```

---

## Modes

### Interactive mode (default)

Start the wrapper without arguments to enter REPL mode.  
Every question is forwarded to the native Copilot CLI and the Q&A pair is
automatically logged.

```bash
dotnet run --project src
```

```
╔══════════════════════════════════════════════════╗
║        GitHub Copilot CLI Wrapper v1.0           ║
╚══════════════════════════════════════════════════╝
Type your prompt or a wrapper command. Type 'help' for help.

> How do I authenticate with OAuth?
[Copilot CLI output appears here and is saved to the log]

> history
📜 History (1 entries):
  #1 [14:30:47] [default] How do I authenticate with OAuth?

> exit
📊 Session Summary
  Total Q&A: 1
  Log saved: ~/.copilot-wrapper/logs/session_2026-03-28_14-30-45.md
```

### Non-interactive single prompt

```bash
dotnet run --project src -- -p "How do I fix a merge conflict?"
```

### Specify a model

```bash
dotnet run --project src -- -m sonnet-4.6 -p "Explain async/await in C#"
```

### Override CLI path

```bash
dotnet run --project src -- --cli-path /custom/path/to/copilot -p "question"
```

### Export history

```bash
# Export previous session logs as JSON
dotnet run --project src -- --export json

# Export as CSV
dotnet run --project src -- --export csv

# Export as Markdown (default)
dotnet run --project src -- --export md
```

### Search history

```bash
dotnet run --project src -- --search "oauth"
```

### Help

```bash
dotnet run --project src -- -h
```

---

## Built-in wrapper commands (interactive mode)

| Command | Description |
|---------|-------------|
| `history` | Display all Q&A pairs from the current session |
| `search <term>` | Find entries whose question or answer matches `<term>` |
| `export [json\|csv\|md]` | Export history to a file (defaults to Markdown) |
| `model [name]` | Show current model or switch to a new one |
| `clear` | Clear in-memory history (log file is unaffected) |
| `exit` | Exit with session summary |
| `help` | Show command reference |

Any other input is forwarded verbatim to the native Copilot CLI.

---

## Environment variables

| Variable | Description |
|----------|-------------|
| `COPILOT_CLI_PATH` | Override the path to the Copilot CLI binary |

---

## Log files

Logs are stored in `~/.copilot-wrapper/logs/` by default.

Each session creates a new file named `session_<timestamp>.md`.  
Export commands create files named `export_<timestamp>.<ext>`.

### Example log

```markdown
---

## 📅 Sessão: 2026-03-28 14:30:45
**Modelo:** sonnet-4.6
**Usuário:** alice
**Plataforma:** Linux

### ❓ Pergunta
> How do I authenticate with OAuth?

### ✅ Resposta
OAuth is an open standard for delegation of access...

**🕐 14:30:47** | **Model:** sonnet-4.6

---

## 📊 Resumo da Sessão
- **Total de Q&A:** 1
- **Encerrada em:** 2026-03-28 14:30:55
```

---

## ♾️ Infinite-loop detection (v2.0)

When the native Copilot CLI enters a continuous-scroll loop the wrapper
detects it automatically, stops collecting output, and saves only the valid
response to the log.

### What you see on screen

```
> How do I fix this error?

[Wrapper streaming CLI output…]

Starting analysis...
Checking dependencies...
Compiling code...

⚠️  Loop detectado após 1 ciclo(s) (confiança: 95%).
✅ Resposta válida capturada e salva no log.
⌨️  Pressione Ctrl+C para interromper o CLI se necessário.

>
```

### What gets saved to the log

Only the content captured **before** the loop begins is written to the log
file.  The repeating scroll output is discarded entirely.

### Detection strategies

| Strategy | Triggers when |
|----------|---------------|
| Exact match | The same sequence of lines restarts (confidence 95 %) |
| Pattern match | Structurally identical lines with varying numbers / e-mails / timestamps / paths (confidence 80 %) |
| High output rate | More than 50 lines/second for more than 50 lines (confidence 70 %) |

See [LOOP_DETECTION.md](LOOP_DETECTION.md) for full technical details.

