# Flash Mem Development Plan (Git-Flow + TDD)

## Branch Strategy
- `codex/main`: production-ready releases only.
- `codex/develop`: integration branch.
- `codex/feature/*`: feature branches from `codex/develop`.
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
  - global `Ctrl` double-tap trigger (SharpHook)
  - popup position at cursor right-bottom with screen-edge clamp
  - border-only frameless popup (no close button)

## Current Gap vs memo-ui-state-spec
- tray/menu-bar integration is not yet implemented.
- Hotkey/settings UI persistence is not yet implemented.

## Next Iteration (Recommended)
1. Add tray/menu bar service.
2. Add settings store and UI for hotkey customization.
3. Store secure password in OS keychain/credential vault instead of env var fallback.
4. Add integration tests for state transitions around popup show/hide.
