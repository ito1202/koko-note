# Quality Gate Best Practices

## Required Gate Sequence
1. `dotnet restore`
2. `dotnet build --no-restore`
3. `dotnet test --no-build`

## Policy
- Treat gate failure as release blocker.
- Stop at the first failing gate and fix root cause.
- Re-run full required sequence after fixes.
- Do not mark tasks complete without current-run gate evidence.

## Optional Strict Gate
- `dotnet format --verify-no-changes` when repository policy requires formatting lock.

## Failure Reporting Format
- `restore: pass|fail`
- `build: pass|fail`
- `test: pass|fail`
- `format: pass|fail|skipped`
- Include first actionable error with file path.

## This Repository Status (latest run)
- restore: pass
- build: pass
- test: pass
- format: skipped
