# Review and load testing

Run correctness checks first, then local load tests, then a small production smoke test. A virtual user (VU) is one concurrent scripted workflow, not one visitor per day. The scripts exercise the API; they do not measure browser rendering, layout or Core Web Vitals.

## 1. Prepare an isolated local database

**A localhost API can still use your production Supabase connection string. Check the effective configuration before running gameplay tests.** Environment variables and user secrets can override appsettings. Do not print or commit credentials when checking this.

Use a separate PostgreSQL test database with the basketball catalog (players, teams, seasons and their relationships) and manually created test accounts. Do not copy real accounts, refresh tokens, contact messages or audit records. Match production's schema and use representative catalog volume.

The runners never create databases, import data or apply migrations. If the test database needs migrations, apply them yourself after verifying the target connection. No new migration is required by this test kit.

Guest and practice workloads write attempts and audit events. The audit table is append-only, so do not use the real database and plan to delete these records afterward. `-ConfirmTestDatabase` acknowledges that you already checked the isolation; it cannot verify the connection string inside the API.

## 2. Review correctness and build

From the repository root, in PowerShell:

```powershell
Set-Location C:\Users\Nemanja\Documents\EuroleagueQuiz
dotnet test .\EuroleagueQuiz.Tests\EuroleagueQuiz.Tests.csproj --configuration Release
```

Build the frontend in a separate terminal:

```powershell
Set-Location C:\Users\Nemanja\Documents\EuroleagueQuiz\client
npm ci
npm run build -- --configuration production
```

There is currently no Angular test target configured, so `npm test` is not a substitute for the build and browser checks.

Check these flows manually against the local test database:

- Guest: all four daily grid levels and the other five games; completion, reveal, resume after refresh, scores and no leaderboard eligibility.
- Registered player: separate practice and daily ranked games, all grid levels, points and leaderboard totals. Repeated submit/refresh must not award points twice.
- Grid: nine valid cells; ambiguous placement; a player who is the sole option in some cells; prevent reuse; green guesses and red reveals. Reveal must not congratulate the user.
- Starting Five: the introductory start dialog, 24-second rounds, five slots, next-round behavior and completion only after round five.
- Search: accents, first/last names, keyboard navigation, selecting a suggestion, retained focus and mobile keyboard visibility.
- Audit: guest and registered start/completion events for each game, no duplicate completion after retries, date filters, user name search, guest summary and pagination. Check API logs for audit-write failures even if the game response is HTTP 200.
- Accounts: login/logout, Google cancellation, confirmation and password flows using test accounts. Ordinary users must receive 403 for admin endpoints, and unauthenticated requests must receive 401 where authentication is required. Do not mass-send emails as a load test.

## 3. Run the local API without the debugger

In the API terminal, after confirming the isolated database:

```powershell
dotnet run --project .\EuroleagueQuiz.Api --configuration Release --launch-profile http
```

The HTTP profile listens on `http://localhost:5248`. Keep this terminal open. The preflight checks use `/api/config/public` and `/api/teams`; there is no dedicated health endpoint today.

## 4. Install portable k6 once

From the repository root:

```powershell
.\scripts\load-tests\Install-K6.ps1
```

This downloads the official Windows x64 k6 v2.4.0 ZIP, verifies its SHA-256 against the official release checksum, and extracts it into ignored `.tools/k6`. It needs no administrator rights or PATH changes. See the [official installation documentation](https://grafana.com/docs/k6/latest/set-up/install-k6/). If local PowerShell execution policy blocks the script, run it with `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\load-tests\Install-K6.ps1`; this only changes policy for that process.

## 5. Run read-only tests first

In another PowerShell terminal at the repository root:

```powershell
# Three workflows: teams, player autocomplete and today's leaderboard.
.\scripts\load-tests\Run-LoadTest.ps1 -Profile smoke

# Gradually increase to 10 concurrent workflows, then ramp down (~5 minutes).
.\scripts\load-tests\Run-LoadTest.ps1 -Profile load -MaxVus 10

# Only after the smaller test passes and memory/database usage look healthy.
.\scripts\load-tests\Run-LoadTest.ps1 -Profile stress -MaxVus 25
```

Load/stress tests ramp through roughly 5, half the requested maximum, and the maximum VUs, then ramp down. Stress holds the peak for longer (~5.5 minutes total). These are bounded concurrency tests, not proof of the application's breaking point. Default simulated think time is one second between actions. Do not compare runs with different think times as if they generated the same load.

## 6. Test game creation, completion and auditing locally

After the read-only tests pass, with the isolated database still confirmed:

```powershell
# Nine journeys: four grid difficulties and the other five games.
.\scripts\load-tests\Run-LoadTest.ps1 -Profile smoke -Workload guest -ConfirmTestDatabase

# Start modestly; this creates new synthetic guests and real test-database writes.
.\scripts\load-tests\Run-LoadTest.ps1 -Profile load -Workload guest -MaxVus 5 -ConfirmTestDatabase
```

Each guest journey starts and finishes a game, then resumes it to check the completed state. Synthetic guest IDs use the `c0dec0de-` prefix. Each journey has a different identity, avoiding accidental serialization of every VU behind one daily-attempt lock. The script uses reveal/skip/submit to complete journeys; it does not replace correctness tests of successful guessing and scoring.

For registered practice, sign in **once** with a confirmed local test account. The credential prompt keeps its password out of command history:

```powershell
$testCredential = Get-Credential -Message 'Local test account: enter the email as username'
$loginBody = @{
    email = $testCredential.UserName
    password = $testCredential.GetNetworkCredential().Password
} | ConvertTo-Json
$env:AUTH_TOKEN = (Invoke-RestMethod -Method Post `
    -Uri 'http://localhost:5248/api/auth/login' `
    -ContentType 'application/json' -Body $loginBody).accessToken
$loginBody = $null
$testCredential = $null

.\scripts\load-tests\Run-LoadTest.ps1 -Profile smoke -Workload practice -ConfirmTestDatabase
.\scripts\load-tests\Run-LoadTest.ps1 -Profile load -Workload practice -MaxVus 5 -ConfirmTestDatabase

Remove-Item Env:\AUTH_TOKEN
```

Practice requests use that test account but separate round/session IDs. Grid practice finishes by revealing all nine cells. Starting Five finishes all five rounds. Use `-Game grid`, `-Game team-history`, `-Game missing-club`, `-Game career-order`, `-Game mystery-player` or `-Game five-player` to isolate a slow game. Grid smoke covers all four difficulties.

To review audit-query performance, sign in once with a local test **Admin** account using the same token procedure, then run:

```powershell
.\scripts\load-tests\Run-LoadTest.ps1 -Profile smoke -Workload admin
.\scripts\load-tests\Run-LoadTest.ps1 -Profile load -Workload admin -MaxVus 5
Remove-Item Env:\AUTH_TOKEN
```

Admin tests only read audit lists, the guest summary and a registered-actor search. Use representative synthetic audit volume; a nearly empty table cannot demonstrate production query performance.

There is intentionally no ranked write-load workload. Concurrent requests from one account hit the same user/day attempt and advisory locks, and do not model many ranked players. A future ranked load test needs a pool of separate confirmed test accounts, isolated daily challenges and assertions against their final scores. Check ranked correctness manually now.

Do not put tokens in script files, Git, screenshots or shared reports. Tokens are read from the process environment and are not added to the runner's command arguments or summaries. Reauthenticate before a run if the token expires.

## 7. Read results and monitor the application

For exact CPU/RAM capture commands, live PostgreSQL checks and audit reconciliation, follow [Monitor a local load test](monitoring-load-tests.md).

Each run writes an ignored `load-test-results/<UTC timestamp>-<profile>-<workload>/summary.json` and a terminal summary. Record the Git commit, database size, server resources and test profile alongside results. Compare the same workload between releases.

Initial targets, to adjust after establishing your baseline:

| Measurement | Initial target |
| --- | --- |
| Failed HTTP requests / failed journeys | Less than 1% |
| Player autocomplete p95, read workload | Less than 1 second |
| Warm game-start p95, guest/practice workload | Less than 3 seconds |
| Overall request p95, load phase | Less than 3 seconds |
| Correctness checks | More than 99% |
| HTTP 429 | Less than 1%; investigate throttling separately |

Smoke checks correctness without latency thresholds. Load/stress apply latency thresholds; sustained request/server-error rates also trigger early aborts. Threshold failures produce a nonzero exit code. See [k6 thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/). Optional environment overrides: `P95_MS`, `SEARCH_P95_MS`, `START_P95_MS`; change these only to reflect an agreed target, not to hide failures.

Run smoke first to warm caches; record the first cold game-start timing separately. The summary distinguishes preflight requests with `phase:preflight`, while thresholds use `phase:load`. Aggregate displayed timing can still include preflight. The first local daily challenge may need more computation than later requests that reuse the generated challenge. One process's warm-up does not warm another process.

While testing Docker locally or later on the server, watch these in separate terminals:

```bash
docker stats
docker compose logs -f --tail=100 api
```

On the Linux server also watch:

```bash
free -h
vmstat 1
sudo tail -f /var/log/nginx/error.log
```

Watch Supabase/PostgreSQL connection usage and query latency too. Stop with Ctrl+C if you see persistent 5xx/504s, OOM/restarts, rising database wait time, exhausted connection pools or memory that keeps increasing after traffic stops. Avoid running the load generator on the same small server you are measuring.

HTTP 429 means a limiter rejected requests. Login, registration, email, contact, import and admin actions have separate limits; Cloudflare/Nginx may impose additional ones. Do not disable production protections or hammer login, registration, Turnstile, Google, email or import endpoints during capacity testing.

## 8. Production: begin with a small read-only smoke test

First reproduce a production-like Docker/Nginx deployment locally or on staging with a separate database. API-only local results exclude container limits, reverse proxies, network latency, Supabase latency and Cloudflare.

Once local checks pass, monitor the server and run this from your own computer during quiet traffic:

```powershell
.\scripts\load-tests\Run-LoadTest.ps1 `
    -Profile production-smoke `
    -BaseUrl 'https://pickandroll.io/api' `
    -AllowProduction
```

This uses one VU for 30 seconds, at least one second between actions, and only reads teams, autocomplete and leaderboard. It does not create guest attempts, audit game events or scores. Other remote targets and production write/stress workloads are rejected before requests are sent. `-MaxVus` does not increase this production profile's one-VU cap.

This is a smoke test, not a production capacity test. Choose a larger production plan only after reviewing local/staging results, server CPU/RAM, expected concurrent players, provider limits, alerts and a quiet test window. There is no automated production load increase in this kit.

## Review findings to resolve before long stress/soak runs

1. `TeamHistoryGameService.Rounds`, the three dictionaries in `CareerGamesService`, and `FivePlayerGameService.Games` retain practice state without expiry/removal. Repeated practice starts can grow process memory. The bounded audit-session tracker does not remove these gameplay dictionaries. Add expiry/bounds before long soak tests.
2. Practice state lives in the API process. Restarts lose it and multiple replicas do not share it. Plan shared state or explicit routing/session behavior before scaling horizontally.
3. Daily attempt transactions intentionally lock by player/guest, game and date. Reusing one guest ID or ranked account across all VUs measures lock contention for that identity instead of many users.
4. Audit completion is best-effort in some paths: HTTP success alone is not evidence that every audit event was saved. Reconcile expected test starts/completions and inspect error logs.

No migrations are applied by any command in this guide or by the load-test scripts.
