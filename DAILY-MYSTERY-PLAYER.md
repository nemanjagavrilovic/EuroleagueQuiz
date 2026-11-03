# Daily Mystery Player

- One shared player and five fixed clues per UTC date, with independent progress for each guest and each game.
- A correct guess with 1/2/3/4/5 visible clues earns 100/80/60/40/20 points. Revealing another clue is free to submit but reduces the points available. Earned score remains zero until a correct guess.
- A wrong guess reveals the next clue. At the fifth clue, a wrong guess or Reveal player ends the attempt, reveals the answer, and earns zero points.
- Refreshes and API restarts restore progress and final score. Completed attempts cannot be replayed. Reset: 00:00 UTC. Guest scores are displayed and saved but excluded from the leaderboard.
- Already guessed players are removed from autocomplete. Repeating a player, invalid player IDs, malformed commands and stale challenge IDs do not reveal additional clues. Retrying a command does not apply it twice.

## Persistence and endpoints

The existing `DailyGameChallenges` and `DailyGameAttempts` tables are reused with game key `mystery-player`. The player, clues, order and scoring version are snapshotted on the server. Unrevealed clues and the answer are not returned to the client before they become available. PostgreSQL transaction locks protect shared challenge creation and individual progress across API instances.

The frontend uses the shared `X-Guest-Id` browser UUID. Clearing storage or switching browsers can bypass guest limits; authenticated rankings remain a later feature.

- `POST /api/career-games/mystery-player/daily/start` — create or resume today's attempt.
- `POST /api/career-games/mystery-player/daily/guess` — `{ challengeId, commandId, playerId?, skip }`. The command ID is a fresh UUID for each action and reused for retries of that action.

Responses contain `round`, `score`, `availablePoints`, `usedPlayerIds`, date/reset time and `leaderboardEligible: false`. Lost-response recovery reloads saved state before enabling further actions.

The original random Mystery Player endpoints remain available for Flutter. Other daily games retain their existing behavior.

## Deployment

No additional migration is needed if `20261101223145_DailyTeamHistory` is applied. Otherwise run from the solution folder with the intended PostgreSQL connection:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Deploy the updated API and Angular client together. Daily creation needs a player with at least three imported historical clubs.
