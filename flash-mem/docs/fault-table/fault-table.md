# Fault Table

| id | date | project | failure | impact | root_cause | prevention_rule | detection | owner | status |
|---|---|---|---|---|---|---|---|---|---|
| FT-0001 | 2026-02-14 | repository | Requirements were captured, but recurrence-prevention source was not standardized. | Same class of mistakes can reappear across tasks. | No mandatory pre-task review source existed. | At task start, always show relevant rows in this fault table and apply the prevention rule during implementation. | Task kickoff checklist | team | active |
| FT-0002 | 2026-02-14 | flash-mem | Template generation blocked on package restore during project bootstrap. | Setup stalled and reduced implementation throughput. | Network-bound restore was coupled to template creation command. | During scaffold phase, generate projects with `--no-restore`, then run restore/build in a dedicated step with clear timeout handling. | Bootstrap command exceeds expected time window | team | active |

## Entry Guide
- `failure`: observable problem in one sentence.
- `root_cause`: why it happened.
- `prevention_rule`: concrete rule that can be followed every time.
- `detection`: how to notice recurrence quickly.
- `status`: `active` or `retired`.
