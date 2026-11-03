# Daily Missing Club

- One shared player, three known clubs, and four fixed answer options per UTC date. Each guest has one scored answer, independent of Grid and Team History.
- Correct answer: 100 points. Wrong answer or Reveal: 0. Either submitting or revealing completes the daily attempt. Completed results cannot be replayed or changed.
- The missing club is the only option from the player's career. Distractors exclude every club the player represented, including clubs outside the three visible clues.
- Refreshing and API restarts restore the challenge and completed score. Reset is 00:00 UTC. Guest scores are displayed but never leaderboard eligible.

## Storage and endpoints

The existing `DailyGameChallenges` and `DailyGameAttempts` tables are reused with game key `missing-club`. The server snapshots the player, clues, option order, answer, and scoring version. Client responses hide the answer until completion. PostgreSQL transaction locks protect concurrent starts and submissions; the first completed result is final, including retried commands.

Requests use the existing `X-Guest-Id` browser UUID. Clearing storage or using another browser can bypass guest limits; authenticated ranked play is a later feature.

- `POST /api/career-games/missing-club/daily/start`: create/resume today's challenge.
- `POST /api/career-games/missing-club/daily/guess`: `{ challengeId, commandId, teamId?, skip }`. A guess must use an offered club ID. Reveal uses `skip: true`. The command ID is a UUID.

Responses include `round`, `score`, `selectedTeamId`, `date`, `resetsAtUtc`, and `leaderboardEligible: false`. Stale challenges, invalid choices and malformed command IDs are rejected without completing the attempt.

The original random endpoints remain available for Flutter. Career Order and Mystery Player retain their existing behavior.

## Deployment

No additional schema migration is needed if `20261101223145_DailyTeamHistory` has already been applied. If not, run from the solution folder using the intended PostgreSQL connection:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Deploy the updated API and Angular client together. The first new challenge requires a player with at least four historical clubs and at least three other clubs in the imported catalog for the distractors.
