# Daily Grid — guest scoring, version 1

Grid is the first game converted to daily challenges. Other games keep their current behavior.

## Rules

- One attempt per guest/browser per UTC date, across all difficulties. The first selected difficulty is locked for the day. Opening another difficulty resumes the existing attempt and updates the URL.
- Guests choosing the same difficulty share the same daily grid. Different difficulties have different daily grids; future rankings must compare equal difficulty or define a common ranked difficulty.
- Unlimited player guesses within the daily grid. Incorrect guesses do not end the attempt or deduct points.
- 10 points per correctly filled cell, plus 10 points for all nine, maximum 100. Revealed cells earn nothing. Guest scores are visible but never leaderboard eligible.
- Players cannot be guessed twice. If a guess fits one empty cell, fill it. If it fits several and is the only possible player for one of them, fill all matching empty cells. Otherwise save the pending guess until the player chooses a highlighted cell.
- The attempt ends only when all nine cells are correctly filled or the player uses Reveal. Reveal preserves earned points. Attempts previously ended by the removed guess cap resume automatically, preserving correct answers; explicitly revealed attempts remain final.
- Refreshes and API restarts preserve the puzzle, guesses, score, pending choice, and completion state. An open tab cannot submit yesterday's challenge into today's attempt.
- Reset: 00:00 UTC. A refresh after reset loads the next challenge.

## Implementation

Controller → daily-grid service → repository → PostgreSQL. The challenge snapshots the grid and candidate players, so a data import cannot change an existing day's accepted answers. Candidate answers remain on the server until guessed or revealed.

`DailyGridChallenges` has a unique date/difficulty index. `DailyGridAttempts` has a unique guest/date index. PostgreSQL transaction-scoped advisory locks serialize changes for a guest across tabs/API instances, and serialize first-time challenge creation. Guess command IDs make retries idempotent. Scoring version 1 is stored with the challenge.

The browser stores an anonymous UUID in `localStorage` (`pickandroll.guestId`) and sends it as `X-Guest-Id`. Clearing storage or changing browsers bypasses guest limits; guest play is not a verified identity. Storage-blocked browsers retain an identity only while the app remains open.

## Database update

Stop the local API if it locks build files. From the solution folder, using the intended PostgreSQL connection configuration:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Migration: `20261101004859_DailyGridAttempts`. Apply it before running the new frontend/API together. No existing player or club rows are modified by this migration.

## Endpoints

All requests require `X-Guest-Id` (a UUID). Responses contain the complete public attempt state, score, date/reset time, pending cells, and accepted/revealed cells.

- `POST /api/grid/daily/start?difficulty=Normal` — create or resume today’s attempt.
- `POST /api/grid/daily/guess` — `{ challengeId, playerId, commandId }`, with a new UUID command ID for each guess.
- `POST /api/grid/daily/place` — `{ challengeId, row, column }`.
- `POST /api/grid/daily/reveal` — `{ challengeId }`.

The existing random-grid endpoints remain for compatibility with the Flutter app. They do not create or score daily attempts. Account registration, practice/ranked selection, leaderboard entries, and multiplayer are not implemented in this phase. Future ranked play must require authenticated server identity and reject guest replay/score promotion.
