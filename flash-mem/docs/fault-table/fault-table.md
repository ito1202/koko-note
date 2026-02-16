# Fault Table

| id | date | project | failure | impact | root_cause | prevention_rule | detection | owner | status |
|---|---|---|---|---|---|---|---|---|---|
| FT-0001 | 2026-02-14 | repository | Requirements were captured, but recurrence-prevention source was not standardized. | Same class of mistakes can reappear across tasks. | No mandatory pre-task review source existed. | At task start, always show relevant rows in this fault table and apply the prevention rule during implementation. | Task kickoff checklist | team | active |
| FT-0002 | 2026-02-14 | flash-mem | Template generation blocked on package restore during project bootstrap. | Setup stalled and reduced implementation throughput. | Network-bound restore was coupled to template creation command. | During scaffold phase, generate projects with `--no-restore`, then run restore/build in a dedicated step with clear timeout handling. | Bootstrap command exceeds expected time window | team | active |
| FT-0003 | 2026-02-14 | flash-mem | Tray icon XAML used unsupported property path and failed compile. | Build failed late in integration phase. | Attached property syntax for Avalonia tray icons was misapplied (`Application.TrayIcons`). | For Avalonia tray integration, use `TrayIcon.Icons` attached property and compile immediately after UI markup edits. | AVLN2000 XAML compile errors on tray properties | team | active |
| FT-0004 | 2026-02-16 | flash-mem | App crashed on startup when encrypted notes password did not match runtime password. | Immediate startup failure and no user path to recover. | `AuthenticationTagMismatchException` from AES-GCM was unhandled in startup async flow. | Always handle decryption mismatch in startup, preserve original encrypted file, and continue with explicit recovery path. | Runtime exception includes `AuthenticationTagMismatchException` | team | active |

## Entry Guide
- `failure`: observable problem in one sentence.
- `root_cause`: why it happened.
- `prevention_rule`: concrete rule that can be followed every time.
- `detection`: how to notice recurrence quickly.
- `status`: `active` or `retired`.
