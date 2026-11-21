# Monitor a local load test

Use the same isolated database and Release API process as the load-testing guide. None of these tools changes database data, installs extensions, resets statistics, applies migrations or sends test traffic to production.

## Capture API CPU and memory

Start the API in one terminal. In a second PowerShell terminal, from the repository root:

```powershell
.\scripts\load-tests\Watch-ApiResources.ps1 -DurationSeconds 480
```

The sampler identifies `EuroleagueQuiz.Api` (or the process listening on port 5248), prints samples every two seconds and writes `api-resources.csv` plus `resources-summary.json` to a new ignored folder under `load-test-results`. If automatic discovery fails, find the API PID in Task Manager > Details, then run `Watch-ApiResources.ps1 -ApiProcessId 12345 -DurationSeconds 480` using its real PID. Monitoring a Docker container requires the Docker instructions below instead.

In a third terminal, start the load test after 20-30 seconds of idle baseline:

```powershell
.\scripts\load-tests\Run-LoadTest.ps1 -Profile stress -Workload guest -MaxVus 50 -ConfirmTestDatabase
```

Leave the sampler running for 60-120 seconds after the test. A normal test fits within eight minutes. Use Ctrl+C if you need to stop it early; CSV rows are flushed after every sample. Sample again at 25 VUs with the same profile and other settings for a cleaner comparison. Do not run the 25/50 VU tests simultaneously.

`CpuPercentTotal` measures API CPU as a percentage of all logical processors visible to the monitoring process. `CpuPercentOneCore = 100` means one logical processor worth of CPU; it can exceed 100. A single hot thread can limit throughput while total CPU is low. `WorkingSetMB` is resident process memory, and `PrivateMemoryMB` is private committed process memory; neither is the .NET managed heap alone. See [Get-Process](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-process?view=powershell-5.1).

Also keep Task Manager > Performance open to observe whole-machine CPU, memory and disk activity, including k6, the database and development tools. The load generator on this PC can affect local results. In production run k6 from another machine.

Interpret CPU and memory together with latency: sustained high API CPU plus rising latency suggests CPU pressure; low API CPU plus rising latency suggests checking waits, database/network latency and pools. An elevated memory plateau after traffic stops can be normal runtime caching/reserved memory. Repeated runs with continued upward growth deserve investigation. These CSVs alone do not prove a memory leak; use runtime/heap profiling after narrowing the problem. Practice-state dictionaries already have a known retention issue.

## Observe PostgreSQL during the run

In pgAdmin, choose the same test database as the API, open Query Tool, and open `scripts/load-tests/database-diagnostics.sql`. If this database is on Supabase, its SQL Editor works too. Keep autocommit on. Select and execute each numbered SELECT separately, about every 5-10 seconds during peak traffic, and export representative results. Running it after the load ends misses transient waits.

1. Connection summary: record active, idle, idle-in-transaction and total client connections. Idle pooled connections alone are normal. The server connection ceiling is distinct from Supabase's pooler ceiling and the application's client pool.
2. Active sessions: look for sustained wait events and long transaction/query ages.
3. Blocking: no rows means no block observed at that instant. Repeated waiting rows, especially growing query ages, identify a problem to investigate. The query includes advisory-lock blocking used by daily attempts.
4. Query-statistics availability: if `pg_stat_statements` is already installed, uncomment the optional query, qualify its schema if needed, and export results before/after the run. Compare increases in calls and execution time. Counters are cumulative; their lifetime mean is not the mean for this test. Do not install/reset anything just to run the diagnostics.

PostgreSQL activity visibility depends on database permissions, and sampling has overhead. The [statistics documentation](https://www.postgresql.org/docs/16/monitoring-stats.html) and [`pg_blocking_pids`](https://www.postgresql.org/docs/16/functions-info.html) explain these views. Empty blocking snapshots do not exclude waits between samples. Normalized SQL text may include literals; inspect before sharing.

If using Supabase, also open the project's Database Reports for CPU, memory, connections and disk I/O over the test window, and its Query Performance view if available. Inspect the specific database project used by this API. See [Supabase Reports](https://supabase.com/docs/guides/observability/reports). The Supabase API/PostgREST response-time chart does not measure this .NET API's requests.

Look at API output for `Timeout`, `Npgsql`, `connection pool`, retry messages and exceptions. PostgreSQL snapshots cannot measure requests waiting in the API's client-side pool; low database activity can coexist with client-pool exhaustion. Do not increase pool limits before finding the source of waits.

## Verify saved guest audit events after the run

Open `scripts/load-tests/verify-guest-audits.sql` in the same database. Set the explicit UTC window to cover exactly one test; result folder names are UTC. Its initial example covers `20261119-201722-995-stress-guest`.

The query restricts results to synthetic guest IDs beginning with `c0dec0de-`, and reconciles actual database attempts with their expected audit events. It detects attempts missing both events, missing/duplicate starts, missing/duplicate completions and premature completion events.

For the completed 50-VU guest run, expect the TOTAL row to show **1,724 database attempts, 1,724 completed attempts, 1,724 started audit events and 1,724 completed audit events**. All missing/duplicate/unexpected columns should be zero. Expect grid 757, team-history 198, missing-club 193, career-order 191, mystery-player 193 and five-player 192 attempts. Verify `completedJourneys.count` in the specific k6 report when checking another run.

The guest workload resumes each attempt after completion; that must not generate a second start event. If counts differ, check the time window and target database first, then inspect attempt and audit behavior. This reconciliation assumes no overlapping tests and that synthetic attempts have not been played again later. After a failed/aborted run, started attempts can legitimately exceed completed journeys.

For practice, inspect Admin > Audit logs using the test user's identity and PracticeStarted/PracticeCompleted actions. The API's technical output can include `Audit event ... could not be persisted.` even when gameplay succeeded. Guest audit records are saved with attempts through the EF interceptor; the reconciliation additionally verifies their expected presence.

## Monitor Docker on the server later

Use a separate SSH terminal while the explicitly bounded production smoke test runs from your PC:

```bash
cd /home/EuroleagueQuiz
docker stats
```

In another terminal:

```bash
cd /home/EuroleagueQuiz
docker compose logs -f --since=10m api
```

Host diagnostics: `free -h`, `vmstat 1` (ignore its first, since-boot averages row), and `sudo tail -f /var/log/nginx/error.log`. `docker compose ps` and `docker inspect --format '{{.State.OOMKilled}} {{.RestartCount}}' "$(docker compose ps -q api)"` help identify OOM/restarts. Docker CPU percentage uses approximately 100% per logical CPU; compare carefully with the normalized Windows sampler. Restart counts and OOM status need before/after context.

Share the k6 `summary.json`, sampler CSV/summary, connection/blocking snapshots and audit reconciliation output from the same time window. No passwords, tokens or connection strings are needed.
