# Daily Career Order

- One shared player and four shuffled clubs per UTC date, with one scored submission per guest. Daily attempts are independent of the other games.
- Clubs are ordered by the player's first recorded EuroLeague season with each club, not by later returns. The daily generator selects clubs with different first seasons to avoid ambiguous season-level ties. The initial shuffle is not already correct.
- Four clubs form six pairs. Score = correctly ordered pairs / 6 × 100, rounded to whole points: 0, 17, 33, 50, 67, 83, or 100. Exact order earns 100; reversing every pair earns 0.
- Rearranging is free and autosaves in sequence. The page shows “Saving order…” until the queue finishes; refresh restores the last successfully saved arrangement. Drafts expose no correctness hints, earned points, or correct order.
- Submitting ends the daily attempt and reveals the correct order. Drafts, retries, and later submissions cannot change a completed result. Guest scores are saved and displayed but never leaderboard eligible. Reset: 00:00 UTC.

## API and persistence

The existing `DailyGameChallenges` and `DailyGameAttempts` tables are reused with game key `career-order`. Server-side snapshots preserve the question, correct order, score version, saved arrangement, and final result across API restarts and data imports. Transaction locks serialize attempt updates; command IDs deduplicate retries, including old draft retries.

Requests use the shared `X-Guest-Id` browser UUID. Clearing storage or switching browsers can bypass guest limits; authenticated rankings are a later feature.

- `POST /api/career-games/career-order/daily/start` — create/resume today's attempt.
- `POST /api/career-games/career-order/daily/draft` — `{ challengeId, commandId, teamIds }`.
- `POST /api/career-games/career-order/daily/submit` — same fields, with a new command ID for the final submission.

Each arrangement must include all four offered club IDs exactly once. Invalid permutations and stale challenge IDs are rejected without spending the scored attempt. Responses contain `round`, `score`, `correctPairs`, `totalPairs`, date/reset time and `leaderboardEligible: false`; `correctPairs` and the answer order remain null before submission.

The original random Career Order endpoints remain compatible with Flutter. Other games retain their existing behavior.

## Deployment

No new migration is required if `20261101223145_DailyTeamHistory` is applied. Otherwise run, using the intended PostgreSQL connection:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Deploy the updated API and Angular client together. Daily creation needs a player with clubs spanning at least four distinct first recorded season years.
