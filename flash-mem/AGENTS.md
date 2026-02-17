# Repository Agent Rules

## Skills In Use
- `skill-creator` (system): `/Users/serpent/.codex/skills/.system/skill-creator/SKILL.md`
  - Use when creating or updating a skill.
- `skill-installer` (system): `/Users/serpent/.codex/skills/.system/skill-installer/SKILL.md`
  - Use when listing/installing skills from curated sources or repositories.
- `quality-gates` (custom): `/Users/serpent/.codex/skills/quality-gates/SKILL.md`
  - Use for release-quality verification, required gate execution, and fail-fast reporting.

## Recurrence Prevention Workflow (Mandatory)
- Before starting any development task, read and present relevant entries from `docs/fault-table/fault-table.md`.
- During implementation, explicitly apply the listed prevention rules from the fault table.
- Before finishing a task, check whether a new failure pattern was discovered.
- If a new pattern exists, append a new row to `docs/fault-table/fault-table.md` with root cause and prevention rule.
- If no new pattern exists, state that no new recurrence item was added.

## Fault Table Location
- Directory: `docs/fault-table`
- Main file: `docs/fault-table/fault-table.md`

## Quality Gates (Mandatory)
- Before finishing any implementation task, run:
  - `dotnet build FlashMem.sln`
  - `dotnet test FlashMem.sln`
- Do not mark task complete when build/test has errors.
- If environment issues prevent running quality gates, report the blocker explicitly and stop.
