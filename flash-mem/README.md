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

If decryption fails because password doesn't match existing encrypted notes:
- the original `notes.enc.json` is preserved
- app starts with a new recovery store file (`notes.recovery-*.enc.json`)
- set `FLASH_MEM_PASSWORD` back to the original password to reopen old notes

## Clibor-like Behavior
- App starts as hidden popup process (no regular main window UX).
- Default hotkey:
  - macOS: `Shift` double-tap
  - Windows: `Control` double-tap
- Hotkey can be changed in `Settings`.
- Popup appears at the mouse cursor's right-bottom position.
- When popup is already visible and hotkey is pressed, popup is re-positioned to current cursor.
- Popup is frameless (no close button/title bar), border-only style.
- Clicking outside hides the popup.
- Tray/menu-bar menu provides: `Open Memo`, `Settings`, `Exit`.
- Memo operations:
  - `New` create memo
  - `Delete` remove selected memo
  - `Save` persist immediately
  - auto-save on edit (debounced)

## Supported Hotkey Keys
- `Shift`
- `Control`
- `Alt` (`Option` on macOS)
- `Command` (macOS) / `Meta` (platform key code)

## macOS Permission Note
- Global hotkey detection uses a global keyboard hook.
- On first run, macOS may require **Input Monitoring** permission for the app/terminal.

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
