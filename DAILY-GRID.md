# Player Grid daily challenges

## Rules

- Each guest/browser can play one grid per difficulty per UTC date. Easy, Normal, Hard, and Legend each save their own progress. Guests on the same level share that day's guest puzzle.
- Ranked play requires an account and allows one attempt per difficulty per UTC date. Ranked grids use different club intersections from the guest grids. Guest scores remain separate after sign-in.
- All four ranked levels contribute to the leaderboard. Their maximum contributions are Easy 10, Normal 20, Hard 30, and Legend 40. A level contributes its score out of 100 multiplied by its maximum contribution. For example, 70/100 on Hard earns 21 leaderboard points. Grid contributes up to 100 points toward the 600-point daily total.
- Normal prioritizes clubs with at least five recorded EuroLeague seasons or appearances in at least two of the latest four recorded seasons. It favors multiple answers per cell and players with longer EuroLeague careers.
- Unlimited player guesses within the daily grid. Incorrect guesses do not end the attempt or deduct points.
- 10 points per correctly filled cell, plus 10 points for all nine, maximum 100. Revealed cells earn nothing. Guest scores are visible but never leaderboard eligible.
- Players cannot be guessed twice. If a guess fits one empty cell, fill it. If it fits several and is the only possible player in any of those cells, fill those singleton cells. Otherwise save the pending guess until the player chooses a highlighted cell.
- The attempt ends only when all nine cells are correctly filled or the player uses Reveal. Reveal preserves earned points. Attempts previously ended by the removed guess cap resume automatically, preserving correct answers; explicitly revealed attempts remain final.
- Refreshes and API restarts preserve the puzzle, guesses, score, pending choice, and completion state. An open tab cannot submit yesterday's challenge into today's attempt.
- Reset: 00:00 UTC. A refresh after reset loads the next challenge.

## Implementation

Controller → daily-grid service → repository → PostgreSQL. The challenge snapshots the grid and candidate players, so a data import cannot change an existing day's accepted answers. Candidate answers remain on the server until guessed or revealed.

`DailyGridChallenges` stores a separate puzzle per date, difficulty, and mode. Guest attempts are keyed by guest, date, and challenge. Ranked attempts are keyed by account, date, and challenge. PostgreSQL advisory locks serialize changes across tabs and API instances, and serialize puzzle creation for each date so equivalent layouts cannot be issued for two new challenges. Guess command IDs make retries idempotent.

New challenges use scoring version 2 immediately. The immediate-activation migration upgrades today's and future saved grids to the four difficulty weights, including attempts already played today. Challenges from earlier dates retain scoring version 1 and their previous best-daily-result rule, up to 100 leaderboard points. The stored scores remain out of 100.

The public configuration includes a scoring announcement until three days after the activation date recorded in `GridScoringSchedules`. The Angular banner appears below the header on Home and Leaderboard, remembers dismissal per browser, and expires automatically.

The browser stores an anonymous UUID in `localStorage` (`pickandroll.guestId`) and sends it as `X-Guest-Id`. Clearing storage or changing browsers bypasses guest limits; guest play is not a verified identity. Storage-blocked browsers retain an identity only while the app remains open.

## Database update

Stop the local API if it locks build files. From the solution folder, using the intended PostgreSQL connection configuration:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Apply pending migrations through `20261117204210_ActivateGridWeightsImmediately` before running the updated app. This migration removes the scheduled wait and applies weighted leaderboard points to today's existing grids. Scores from previous dates keep their original scoring rules.

## Endpoints

All requests require `X-Guest-Id` (a UUID). Responses contain the complete public attempt state, score, date/reset time, pending cells, and accepted/revealed cells.

Responses include `scoringVersion`. Eligible ranked responses also include `leaderboardPoints` and `maxLeaderboardPoints`. Guest responses return null for both point fields. The stored score remains out of 100. Leaderboard totals use the scoring version saved with each challenge.

- `POST /api/grid/daily/start?difficulty=Normal` — create or resume today’s attempt.
- `POST /api/grid/daily/guess` — `{ challengeId, playerId, commandId }`, with a new UUID command ID for each guess.
- `POST /api/grid/daily/place` — `{ challengeId, row, column }`.
- `POST /api/grid/daily/reveal` — `{ challengeId }`.

Guest mode is the default. Pass `ranked=true` on each request for authenticated Ranked play. The random-grid endpoints serve Practice mode.
