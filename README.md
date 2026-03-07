# Valkey for Windows

[![Build](https://github.com/valkey-windows/valkey-windows/actions/workflows/build-valkey.yml/badge.svg)](https://github.com/valkey-windows/valkey-windows/actions)
[![Release](https://img.shields.io/github/v/release/valkey-windows/valkey-windows)](https://github.com/valkey-windows/valkey-windows/releases)

Compiled from official Valkey source for Windows.

## Quick Start

```cmd
# After download and extract
valkey-server.exe valkey.conf

# Or use ValkeyService (recommended)
ValkeyService.exe run --foreground
```

## Usage

### Option 1: ValkeyService.exe (Recommended)

Automatically handles path conversion. Use native Windows paths.

```cmd
# Run in foreground
ValkeyService.exe run --foreground --port 6379 --dir C:\valkey-data

# Install as Windows service
ValkeyService.exe install -c C:\config\valkey.conf --dir D:\data\valkey --port 6379
net start Valkey

# Uninstall service
ValkeyService.exe uninstall
```

### Option 2: valkey-server.exe (Direct)

**Important:** This build uses Cygwin runtime. Command-line paths must use Cygwin format.

```cmd
# ✅ Correct - Cygwin path format
valkey-server.exe /cygdrive/c/config/valkey.conf --dir /cygdrive/d/data --port 6379

# ❌ Wrong - Windows paths not supported
valkey-server.exe C:\config\valkey.conf --dir D:\data
```

**Path Conversion:**

| Windows | Cygwin |
|---------|--------|
| `C:\path` | `/cygdrive/c/path` |
| `D:\path` | `/cygdrive/d/path` |
| `.\data` | `./data` (relative works as-is) |

**In config file:** Use forward slashes (Windows style with `/`).

```conf
# Recommended in valkey.conf
dir C:/valkey/data
logfile C:/valkey/logs/valkey.log
```

## ValkeyService CLI Reference

```cmd
ValkeyService.exe [command] [options]

Commands:
  install       Install as Windows service
  uninstall     Uninstall Windows service
  run           Run Valkey (default)

Options:
  -c, --config <FILE>      Config file path
  --port <PORT>            Server port
  --dir <DIRECTORY>        Data directory
  --loglevel <LEVEL>       Log level (debug/verbose/notice/warning)
  -f, --foreground         Run in foreground
  --service-name <NAME>    Service name (default: Valkey)
  --start-mode <MODE>      Startup type (auto/manual)
  -h, --help               Show help
  -v, --version            Show version
```

## Cross-Partition/Directories

Config, data, and program can be in any location:

```cmd
# Program: C:\valkey\ValkeyService.exe
# Config:  D:\config\valkey.conf
# Data:    E:\data\valkey

ValkeyService.exe run -c D:\config\valkey.conf --dir E:\data\valkey --foreground
```

## Data Persistence

Data is saved automatically on shutdown. `ValkeyService.exe` correctly passes `--dir` to ensure data is saved to the specified directory.

```cmd
# Start
ValkeyService.exe run --foreground --dir C:\valkey-data

# Write data
valkey-cli SET mykey myvalue

# Graceful shutdown
valkey-cli SHUTDOWN

# Restart - data persists
valkey-cli GET mykey   # Returns "myvalue"
```

## FAQ

### valkey-server.exe can't find config file?

Use Cygwin path format:
```cmd
valkey-server.exe /cygdrive/c/config/valkey.conf
```

Or use `ValkeyService.exe` which handles path conversion automatically.

### Data lost after restart?

1. Always specify `--dir` option
2. Use graceful shutdown (`valkey-cli SHUTDOWN` or `Ctrl+C`), don't kill the process
3. Use the same `--dir` when restarting

## Technical Details

- Build toolchain: MSYS2 / Cygwin
- Service wrapper: .NET 10.0
- Path handling: ValkeyService auto-converts Windows ↔ Cygwin paths

---

English | [한국어](README.ko_KR.md)

## Disclaimer

This project is not affiliated with, endorsed by, or sponsored by LF Projects, LLC. The license provided here applies only to this repository, not to the official Valkey project.

This is recommended for local development only. For production environments, please follow Valkey official guidance and deploy on Linux. This project is not responsible for any losses caused by its use.
