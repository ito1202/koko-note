# Flash Mem Development Plan (Git-Flow + TDD)

## Branch Strategy
- `master`: production-ready releases only.
- `develop`: integration branch.
- `codex/feature/*`: feature branches from `develop`.
- Current branch: `codex/feature/flash-mem-mvp`.

## Build Setup
1. Install .NET 8 SDK (`dotnet@8`).
2. Use pinned SDK from `global.json`.
3. Restore and build:
   - `dotnet restore FlashMem.sln`
   - `dotnet build FlashMem.sln`

## TDD Workflow
1. Write failing tests in `tests/*`.
2. Implement minimal code in `src/*`.
3. Run `dotnet test FlashMem.sln`.
4. Refactor without changing behavior.

## Implemented in This Iteration
- Domain tests and implementation:
  - memo list selection behavior
  - archive behavior with "never hide the last visible memo"
  - double-tap hotkey detector (`Ctrl` double-tap timing logic)
- Infrastructure tests and implementation:
  - `Argon2id` key derivation + `AES-GCM` encryption
  - encrypted file store with atomic replace save
  - no plaintext leakage test
- Desktop UI:
  - memo list pane (thread-like list)
  - detail pane (title + content)
  - archive button
  - up/down keyboard selection
  - hide on deactivation (outside focus click)

## Current Gap vs memo-ui-state-spec
- Global OS hotkey registration and tray integration are not yet implemented.
- Cursor-right-bottom popup placement on global hotkey trigger is not yet implemented.
- Hotkey/settings UI persistence is not yet implemented.

## Next Iteration (Recommended)
1. Add cross-platform hotkey adapter:
   - macOS: event tap
   - Windows: `RegisterHotKey`
2. Add tray/menu bar service.
3. Add settings store and UI for hotkey customization.
4. Add integration tests for state transitions around popup show/hide.
