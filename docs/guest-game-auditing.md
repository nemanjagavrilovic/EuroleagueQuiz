# Guest gameplay in audit logs

Administrators can open **Audit logs** at `/admin/audit-logs` to see guest gameplay alongside existing account, admin, import, contact and ranked events.

## What is recorded

All six daily games record `Game.<GameName>GuestStarted` and `Game.<GameName>GuestCompleted`. Grid records each of Easy, Normal, Hard and Legend as a separate attempt. Refreshing, polling, resuming or making another guess does not create another start. A completed attempt, including one finished by revealing answers, records a completion even when it earns zero points.

Events are written by `GameAttemptAuditInterceptor` in the same transaction as the daily attempt. Registered daily starts and completions are also audited. Practice games are not included in guest reports; their registered-player events are recorded separately under actor **User**.

Guest events have actor type `Guest`, no account user ID, and a stable `GuestGameAttempt` target key containing the existing browser UUID, game, UTC challenge date and challenge ID. This key is available before a new grid attempt receives its numeric database ID. Details contain only game, challenge date/ID, difficulty, score, mode and leaderboard eligibility. Answer state, IP addresses, contact information and authentication tokens are not stored in these entries.

## Counts and filters

The report uses these saved events, with aggregation performed in PostgreSQL:

| Metric | Meaning |
| --- | --- |
| Unique guests | Distinct guest UUIDs across all games in the selected period. |
| Games played | Distinct guest attempt keys with a recorded start or completion in the period. A start and completion count as one game. |
| Completed games | Distinct attempt keys with a recorded completion in the period, including revealed games. |
| Completion rate | Completed games divided by games played. |

The expandable game table shows unique guests, played games and completed games for each of the six games. Per-game unique counts should not be added to calculate total unique guests: one browser can play several games.

Date filters apply to the guest report and event list. Search, actor, action and result filters apply to the event list only. Choose actor **Guest** to show guest events; the event details include the full guest UUID, which can also be entered in Search. Guest labels show a shortened identifier and do not expose account identities.

Counts represent guest identities playing daily games, not all page visitors or verified people. Clearing browser storage creates a new guest identity; the same person using multiple browsers/devices can be counted more than once. The web and Flutter apps share these API endpoints, so guest traffic from a mobile app using them is included too. Existing guest IDs are client supplied, so these are operational usage figures, not an anti-fraud identity system.

## Rollout

No database schema change or migration is required. Deploy the API and Angular changes together. **No migrations are applied by this feature.**

Collection begins with the updated code. Older attempts are not backfilled or guessed from existing account data. The page shows the first recorded guest activity time. An older unfinished guest attempt that completes after deployment is included as one played and completed game, even if its original start was not logged. The historical audit entry remains a guest event if that browser later registers.

Both audit endpoints require the Admin role and disable response caching:

- `GET /api/admin/audit-logs` supports `search`, `action`, `result`, `actorType`, `fromUtc`, `toUtc`, `page` and `pageSize`. Search includes registered players’ display names and user IDs.
- `GET /api/admin/audit-logs/guest-activity` supports optional `fromUtc` and `toUtc` timestamps. It returns totals, the first tracking timestamp and the six-game breakdown.
