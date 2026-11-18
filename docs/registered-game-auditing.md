# Registered player game auditing

Open **Admin → Audit logs**, select actor **User**, and search a player's display name or user ID. Registered players acting as admins in a game are also recorded as **User** for consistent gameplay filtering; admin account-management actions keep their **Admin** actor type.

All six games have the following events:

| Mode | Start | Finish |
| --- | --- | --- |
| Ranked | `Game.<Game>RankedStarted` | `Game.<Game>RankedCompleted` |
| Practice | `Game.<Game>PracticeStarted` | `Game.<Game>PracticeCompleted` |
| Claimed guest daily attempt | `Game.<Game>DailyStarted` | `Game.<Game>DailyCompleted` |

Game names in action codes are `Grid`, `TeamHistory`, `MissingClub`, `CareerOrder`, `MysteryPlayer`, and `StartingFive`. Ranked and claimed daily events are saved with the daily attempt in its existing database transaction. Completing a claimed daily attempt does not make it eligible for leaderboard points.

A claimed unfinished guest attempt receives a registered daily start when resumed. Claiming an already finished guest attempt does not create a new registered start or completion, and its existing guest history is preserved.

Practice events use the actual server round ID and authenticated account, with a shared, bounded in-memory audit session store. Owners cannot use another registered player's tracked round. Successful completions, wrong final answers, reveals and timed finishes are all recorded. Starting Five records a whole-game completion after round five, rather than after each individual round. Its completion details include the number of correctly guessed players.

Practice polling and repeated completion requests do not create duplicate events. Creating a genuinely new practice round, including by refreshing a page that generates another round, produces a new start. Ranked refreshes resume the same saved attempt and do not produce another start. Only game context/outcomes are stored in audit details, without submitted answers, IP addresses, passwords or contact data.

## Practice grid

Grid placement runs in the clients, so it has a small audit-specific handshake:

1. Authenticated grid creation returns an optional `practiceSessionId` alongside the grid. The definition and session are bound to the signed-in player. Grid responses disable caching.
2. Clients include this ID in `players/grid-match` and `grid/reveal` requests. The API checks that the requested clubs match the session and remembers the validated player/cell matches.
3. After solving all nine cells, clients send `POST /api/grid/practice/{practiceSessionId}/complete` with `{ "answers": [{ "row": 0, "column": 0, "playerId": 123 }, ...] }`. The API verifies all nine distinct cells against its validated matches, including the existing rule for repeated sole-option players. The answer payload is not stored in the audit log.
4. A reveal completes auditing directly on the server. A late completion callback cannot replace a revealed outcome with a solved one.

The Angular and Flutter grid clients both preserve the session ID and submit solved completions with bounded retries. Existing clients that omit the optional fields remain playable, but must adopt this handshake to report solved grid finishes. No practice action awards leaderboard points.

## Deployment and limits

No database schema changes or migrations are needed or applied. Deploy the updated API and Angular application, and release the updated Flutter app for mobile grid completion reporting. Existing ranked completion history stays intact; newly covered events begin after deployment and are not backfilled.

Practice audit sessions last up to 24 hours and are held by the API process, consistent with the current in-memory practice game architecture. Restarting the API clears these sessions; start a new practice round afterward. Keep practice traffic on one API instance until the game and audit session stores are moved to shared storage. Audit delivery uses the existing best-effort writer: database delivery failures are reported through the technical logger without changing completed gameplay into an error.

Guest metrics remain guest-only. Registered events are visible in the paginated event list with account display names, mode-specific actions and the existing date/result filters.
