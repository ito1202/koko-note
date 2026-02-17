# Flash Mem Release Stage Plan

## Stage 1: Core Popup Memo (Done)
- [x] Frameless popup UI (no close button)
- [x] Memo list pane + detail pane
- [x] New/Delete/Save memo commands
- [x] Archive action
- [x] Arrow key navigation
- [x] Hide on focus loss
- [x] Cursor right-bottom placement + edge clamp

## Stage 2: Security and Persistence (Done)
- [x] Encrypted memo storage (`Argon2id` + `AES-GCM`)
- [x] Atomic file save
- [x] Plaintext leak prevention tests

## Stage 3: Resident Operation (Done)
- [x] Global double-tap hotkey
- [x] Reposition-and-redraw on hotkey while popup already visible
- [x] Startup hidden (resident style)
- [x] Tray/menu-bar menu:
  - [x] Open Memo
  - [x] Settings
  - [x] Exit

## Stage 4: Configurability (Done)
- [x] Hotkey setting UI
- [x] Double-tap window setting UI
- [x] Start-at-login toggle
- [x] Settings persisted (`settings.json`)
- [x] Runtime hotkey reload on settings save

## Stage 5: OS Defaults and Compatibility (Done)
- [x] macOS default hotkey: `Shift` double-tap
- [x] non-mac default hotkey: `Control` double-tap
- [x] Supported tap keys:
  - [x] `Shift`
  - [x] `Control`
  - [x] `Alt` / `Option`
  - [x] `Command` (Meta)

## Release Checklist
- [x] `dotnet build FlashMem.sln`
- [x] `dotnet test FlashMem.sln`
- [x] Manual verification on macOS (input monitoring permission + tray behavior)
- [ ] Manual verification on Windows 10/11 (tray + startup registration)

## Validation Note
- As of 2026-02-17, functional validation has been completed on macOS only.
- Windows 10/11 verification remains pending and is required before production release.
