# Daily Starting Five

- Five fixed daily clubs, in the same order for everyone. Each round accepts any five distinct players from that club's snapshotted historical roster, regardless of position or season. Answers are not preselected.
- Correct players earn 4 points each: 20 per round, up to 100 across five rounds. Wrong guesses, duplicates, and revealed players earn zero. A player may count again at a different club if they represented it.
- Each active round lasts 24 seconds. The server owns its UTC deadline. Refreshes, background tabs, disconnects and API restarts do not reset it. Returning after expiry completes that round, preserves correct guesses and reveals only enough players to fill five slots.
- Reveal round ends the current round immediately and keeps earned points. Correct answers stay green; revealed answers stay red after refresh.
- Next round starts the next 24-second clock. Waiting between rounds is allowed until the daily UTC reset. Retries cannot skip rounds or restart their timers.
- After five completed rounds, the result is final until 00:00 UTC. Guest points are displayed and saved but excluded from the leaderboard. Each guest has an independent timer/progress state.

## Persistence and API

The existing `DailyGameChallenges` and `DailyGameAttempts` tables use game key `five-player`. The server snapshots five clubs and their full valid player pools; future clubs and unused players are never sent to the frontend. Attempts store UTC deadlines, accepted/revealed IDs, started rounds and processed command IDs. PostgreSQL transaction locks serialize each guest's actions across tabs/API instances.

All requests use the shared `X-Guest-Id` browser UUID. Clearing storage or switching browsers can bypass guest limits; ranked authenticated play is a later feature.

- `POST /api/five-player/daily/start` — create/resume and synchronize expiry.
- `POST /api/five-player/daily/guess` — `{ challengeId, roundNumber, commandId, playerId?, skip }`.
- `POST /api/five-player/daily/next` — `{ challengeId, roundNumber, commandId }`.

A fresh UUID identifies each action; retries reuse that command ID. An old command retry returns current state. A new command targeting an old round is rejected, preventing delayed guesses or reveals from affecting the next club.

Responses include `round`, point `score`, `correctGuesses`, `serverNowUtc`, `deadlineUtc`, `revealedPlayerIds`, date/reset time and `leaderboardEligible: false`. The nested legacy round's `score` remains a count of correct players; the daily wrapper's `score` is points out of 100. Completed rounds have zero remaining seconds and no active deadline.

The browser derives its display from the server deadline using monotonic time, synchronizes when the tab resumes, and reloads saved state after uncertain responses. Reaching client zero synchronizes the server; it does not send a surrender request. Only a server-confirmed deadline ends the round.

The original random endpoints remain available for Flutter. Other daily games retain their current behavior.

## Deployment

No new migration is required if `20261101223145_DailyTeamHistory` is applied. Otherwise, with the intended PostgreSQL connection configured:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

Deploy the updated API and Angular client together. Daily generation requires at least five clubs with five distinct historical players each.
