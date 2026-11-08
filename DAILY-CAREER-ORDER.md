# Daily Career Order

- One shared player and four shuffled clubs per UTC date, with one scored submission per guest. Daily attempts are independent of the other games.
- Questions use four consecutive club spells in the player's recorded EuroLeague history. Consecutive seasons at the same club form one spell; return visits remain in the timeline. No intermediate recorded club is skipped. A season with multiple clubs breaks the known sequence because the roster feed cannot reliably order those moves. Windows with repeated club choices, or multiple possible sequences for the same four choices, are excluded. The initial shuffle is not already correct.
- Four clubs form three directed consecutive transfers. Score = correct transfers / 3 × 100, rounded to whole points: 0, 33, 67, or 100. Both adjacency and direction must match. For a correct sequence A → B → C → D, B → A → C → D earns 33 because only C → D matches. A → C skips a recorded club and earns nothing; B → A reverses a move and earns nothing.
- Rearranging is free and autosaves in sequence. The page shows “Saving order…” until the queue finishes; refresh restores the last successfully saved arrangement. Drafts expose no correctness hints, earned points, or correct order.
- Submitting ends the daily attempt and reveals the correct order. Drafts, retries, and later submissions cannot change a completed result. Guest scores are saved and displayed but never leaderboard eligible. Reset: 00:00 UTC.

## API and persistence

The existing `DailyGameChallenges` and `DailyGameAttempts` tables are reused with game key `career-order`. Server-side snapshots preserve the question, correct order, score version, saved arrangement, and final result across API restarts and data imports. Transaction locks serialize attempt updates; command IDs deduplicate retries, including old draft retries.

Requests use the shared `X-Guest-Id` browser UUID. Clearing storage or switching browsers can bypass guest limits. Authenticated ranked attempts use the signed-in account; practice attempts remain unranked.

- `POST /api/career-games/career-order/daily/start` — create/resume today's attempt.
- `POST /api/career-games/career-order/daily/draft` — `{ challengeId, commandId, teamIds }`.
- `POST /api/career-games/career-order/daily/submit` — same fields, with a new command ID for the final submission.

Each arrangement must include all four offered club IDs exactly once. Invalid permutations and stale challenge IDs are rejected without spending the scored attempt. Responses contain `round`, `score`, `correctTransfers`, `totalTransfers: 3`, `scoringVersion`, date/reset time and `leaderboardEligible`; `correctTransfers` and the answer order remain null before submission.

Challenge snapshots store `HistoryVersion: 2` for the new generation/scoring rules. Existing daily definitions without this field keep their original scoring, including already completed results and leaderboard totals, until that day's UTC reset. Their responses use `scoringVersion: 1` and `correctTransfers: null`; both clients explain the transition. No historical questions or scores are rewritten.

The original random Career Order endpoints use the same consecutive-spell generator for practice, without ranked points. Angular and Flutter show the new daily scoring feedback. Other games retain their existing behavior.

## Deployment

No new migration is required if `20261101223145_DailyTeamHistory` is applied. Otherwise run, using the intended PostgreSQL connection:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Deploy the updated API and Angular client together, and rebuild the Flutter app. This scoring update requires no additional database migration. Daily creation needs a player with an unambiguous window of four consecutive distinct clubs in the recorded EuroLeague history. Existing saved daily questions switch to the new rules with the next newly generated UTC challenge; practice uses the new generator immediately.
