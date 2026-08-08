# Jetset

Jetset is a lightweight personal Windows desktop utility: a visible clock, stopwatch or countdown timer, and a simple work-session tracker for today.

It helps you keep time in view, measure how long tasks actually take, pause without counting idle time, and review today’s productive total — without accounts, cloud sync, or team time-tracking features.

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet restore
dotnet build
```

## Run

```bash
dotnet run --project src/Jetset.App/Jetset.App.csproj
```

## Test

```bash
dotnet test
```

## Publish (self-contained Windows x64)

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use the publish profile:

```bash
dotnet publish src/Jetset.App/Jetset.App.csproj -p:PublishProfile=win-x64
```

The output is under `src/Jetset.App/bin/Release/net10.0-windows/win-x64/publish/`.

## Database location

SQLite database:

`%LocalAppData%\Jetset\jetset.db`

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+N` | Start new session |
| `Ctrl+P` | Pause or resume |
| `Ctrl+Enter` | Finish active session |
| `Ctrl+M` | Toggle compact mode |
| `Ctrl+H` | Show or hide the main window |

## Features

- Idle / running / paused main-window states
- Stopwatch (active time only) and countdown (absolute end time, overtime after zero)
- One active session at a time
- Today’s history and total productive duration
- Optional session notes and simple history edits
- Always-on-top, compact mode, remember window position/size
- Minimize to system tray (close hides; Exit from tray quits)
- Optional start with Windows
- 12/24-hour clock, optional seconds, light/dark theme
- Interrupted-session recovery after restart

## Solution structure

```text
Jetset.sln
src/Jetset.App/     WPF application
tests/Jetset.Tests/ xUnit timing and session tests
```
