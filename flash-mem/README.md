# Flash Mem

Hotkey-first resident memo app (Windows/macOS) based on the specification in `docs/memo-ui-state-spec.md`.

## Prerequisites
- .NET SDK 8.0.124
- Avalonia templates installed

## Setup
```bash
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
dotnet restore FlashMem.sln
dotnet build FlashMem.sln
dotnet test FlashMem.sln
```

## Run Desktop App
```bash
export FLASH_MEM_PASSWORD="set-a-strong-password"
dotnet run --project src/FlashMem.Desktop/FlashMem.Desktop.csproj
```

## Git-Flow
1. branch from `codex/develop`: `codex/feature/<scope>`
2. implement with TDD in small commits
3. merge feature -> `codex/develop` after tests pass
4. promote `codex/develop` -> `codex/main` for release

## Architecture
- `src/FlashMem.Domain`: memo state and input rules
- `src/FlashMem.Application`: use cases/services
- `src/FlashMem.Infrastructure`: encrypted persistence
- `src/FlashMem.Desktop`: Avalonia UI
- `tests/*`: unit tests by layer
