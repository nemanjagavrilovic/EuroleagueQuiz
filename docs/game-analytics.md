# Game analytics — GA4 and Meta Pixel

The Angular app uses the existing tracking IDs returned by `/api/config/public`.
No additional tags, packages, server endpoints or database migration are needed.
Player Grid keeps its existing event names. The other five games now use the
same start/completion/reveal events, with additional answer, clue and round events.

## Events

| GA4 event | Meta custom event | Meaning |
| --- | --- | --- |
| `quiz_start` | `QuizStart` | First confirmed gameplay interaction; for Starting Five, an active round is loaded after Start or Next round. |
| `quiz_answer` | `QuizAnswer` | A player, club or career order was checked successfully by the API. `result` describes the answer. Not sent for autocomplete typing or failed requests. |
| `quiz_clue_reveal` | `QuizClueReveal` | Team History or Mystery Player adds a clue. `reason` distinguishes a skip from an incorrect answer. |
| `quiz_reveal` | `QuizReveal` | The player explicitly reveals the answer, grid or Starting Five round. |
| `quiz_complete` | `QuizComplete` | A submitted answer finishes a game, or Starting Five finishes its final round naturally/on timeout. This can include an incorrect or partial result. An explicit final reveal does not send this event. |
| `quiz_round_start` | `QuizRoundStart` | A Starting Five round becomes playable after Start or Next round. |
| `quiz_round_complete` | `QuizRoundComplete` | A previously active Starting Five round ends. `reason` is `answered`, `timeout` or `reveal`. |

## Coverage

| Game (`game` parameter) | Start / complete | Answer | Explicit reveal | Extra events |
| --- | --- | --- | --- | --- |
| `grid` | Existing behavior | — | Yes | Difficulty on existing events |
| `team-history` | Yes | Yes | After the final club clue | Clue reveal |
| `missing-club` | Yes | Yes | Yes | — |
| `career-order` | On confirmed submission | Yes | No reveal action in this game | — |
| `mystery-player` | Yes | Yes | After the final clue | Clue reveal |
| `five-player` | Yes | Yes | Per round | Round start / complete |

All modes are covered: `daily` (guest daily), `ranked`, and `practice`.

### Counting rules

- Initial loading of Team History, Missing Club, Career Order or Mystery Player
  does not count as starting gameplay. The first confirmed answer or clue skip
  does; Career Order starts when its order is submitted successfully.
- Loading or refreshing a completed daily result does not replay answer,
  completion or reveal events. Restoring progress after a failed request only
  emits a confirmed transition when the returned state shows one occurred.
- Starting Five starts after its user-triggered countdown and a successful
  response containing an active round. Resuming an unfinished game through
  Start counts as engaging with that game in this visit; it is not a count of
  unique newly-created daily attempts. Background synchronisation does not
  replay starts or answers. It can record a newly observed round timeout.
- `quiz_start` on a later Starting Five round can occur when an initially
  restored completed round is followed by Next round. It is sent once within
  the current component visit.
- A reveal is a separate outcome from completion. Do not add `QuizReveal` to a
  campaign's successful-completion conversion.
- Failed final guesses still complete the non-grid game, with `result=incorrect`.
  Starting Five completion can be `correct`, `partial` or `incorrect`.
- API failure recovery is best effort. If the API cannot tell whether a wrong
  Starting Five guess was accepted, no answer event is invented during sync.
- Practice replays can generate new starts and completions. Browser analytics
  does not prove score eligibility or replace the server's ranked leaderboard.

## Parameters

Both providers receive only explicitly allowed game parameters:

- `game`: the game identifiers in the coverage table.
- `mode`: `daily`, `ranked`, or `practice`.
- `difficulty`: `easy`, `normal`, `hard`, or `legend`, for Grid only.
- `result`: `correct`, `incorrect`, or `partial`, where available.
- `reason`: `skip`, `incorrect`, `reveal`, `timeout`, or `answered`, where applicable.
- `score`: 0–100, from the returned daily/ranked score when available.
  Starting Five practice converts its 0–25 correct-player total to 0–100.
  Other practice games omit scores not provided by their API.
- `clues_revealed`: number of revealed clubs/clues for the two clue games.
- `round_number`: the current Starting Five round, 1–5.
- `correct_players`: current-round count on Starting Five round events;
  cumulative count, 0–25, on its whole-game completion event.

GA4 also receives the existing sanitised `page_location`. No search terms,
guessed names, player/club IDs, quiz answers, challenge IDs, account IDs,
email addresses, phone numbers or arbitrary error messages are sent by these
game-event hooks. Meta retains the existing page URL safety checks.

## Consent and delivery

- GA4 requires the analytics choice; Meta requires the separate marketing choice.
- Actions performed before permission are not replayed after consent is granted.
- Only already-permitted events wait for configuration/tag readiness, in a
  bounded queue. Navigation, withdrawal or an unavailable ID discards its queue.
- Tracker failures must not interrupt answering or changing rounds.
- Ad blockers and network failures can prevent browser analytics delivery.

## Configure reports after deployment

1. Keep the current GA4 measurement ID and Meta Pixel ID. Do not add a second
   copy of their snippets.
2. Accept the relevant optional cookies in your test browser. Check GA4 Realtime
   or DebugView (when a debug session is enabled) and Meta Events Manager's
   Test Events. Use the provider's browser debugging tools if needed.
3. In GA4 Admin → Custom definitions, create event-scoped dimensions for `game`,
   `mode`, `difficulty`, `result`, and `reason`. Add `round_number` if you need
   round breakdowns. Numeric parameters can be registered as custom metrics.
   Custom definitions are needed to use custom parameters in normal reports.
4. Mark `quiz_complete` as a key event if finishing a game is your conversion.
   To measure only a correct finish, create a derived event filtered by
   `result=correct`. For ranked score reports, also filter `mode=ranked`.
5. In Meta, create a custom conversion from `QuizComplete`, optionally filtered
   by `game`, `mode` or `result`. `QuizStart` can be used for an engagement funnel.
6. Compare page visits → starts → completions by game and mode. Keep reveals
   as their own branch. Career Order's confirmed submission creates start and
   completion together, so page visits are its useful abandonment baseline.

References:

- https://developers.google.com/analytics/devguides/collection/ga4/events
- https://developers.google.com/analytics/devguides/collection/ga4/event-parameters
- https://support.google.com/analytics/answer/14240153
- https://developers.facebook.com/docs/meta-pixel/implementation/conversion-tracking/
