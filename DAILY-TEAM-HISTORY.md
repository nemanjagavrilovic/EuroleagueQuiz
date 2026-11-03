# Daily Team History

## Rules

- One shared player and four ordered club clues per UTC date. Guests have one attempt, separate from their daily Grid attempt.
- First clue is visible immediately. A wrong guess or Reveal next club reveals the next clue. At the fourth clue, a wrong guess or Reveal player ends the attempt and reveals the answer.
- A correct guess with one/two/three/four visible clubs earns 100/75/50/25 points. Revealing the answer or missing the final guess earns 0.
- The UI distinguishes earned points from the points available for the next correct guess. Guest scores never enter the leaderboard.
- Refreshing, opening another tab, or restarting the API restores saved progress and final scores. Completed attempts cannot be replayed. A new challenge starts at 00:00 UTC.
- Duplicate command IDs do not apply twice. Repeating an already guessed player, invalid players, malformed commands, and stale challenge IDs do not reveal additional clues.

## Persistence and API

The controller calls `DailyTeamHistoryService`, which uses `IDailyGameRepository` and PostgreSQL. `DailyGameChallenges` snapshots the player, club order, and scoring version. `DailyGameAttempts` saves progress, commands, submitted players, and awarded score. The answer stays on the server until completion.

Unique indexes enforce one challenge per game/date and one attempt per guest/game/date. Transaction-scoped PostgreSQL advisory locks protect challenge creation and attempt updates across API instances. These game-keyed tables can support subsequent daily games without changing Grid's existing tables.

The frontend reuses Grid's `pickandroll.guestId` browser identifier and sends `X-Guest-Id`. Guest limits are browser-based and can be bypassed by clearing storage or switching browsers. Account-based ranked play is not part of this change.

- `POST /api/team-history/daily/start`: create/resume today's attempt.
- `POST /api/team-history/daily/guess`: `{ challengeId, commandId, playerId?, skip }`. `commandId` is a fresh UUID for a new action and must be reused if retrying that same action.

The response includes `round`, `score`, `availablePoints`, `date`, `resetsAtUtc`, and `leaderboardEligible: false`. It does not include unrevealed clubs or the correct player before completion.

The original random Team History endpoints remain compatible with Flutter. They do not create daily attempts or leaderboard scores.

## Apply the migration

From `C:\Users\Nemanja\Documents\EuroleagueQuiz`, with the intended database connection configured:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Migration: `20261101223145_DailyTeamHistory`. Apply it before running the updated web game. No existing player/club data is modified. Restart the API after deploying the updated binaries.
