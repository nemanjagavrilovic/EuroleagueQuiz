-- READ ONLY. Run AFTER a finished test, in the API's isolated test database.
-- Change the UTC window below for the run you want to verify. Folder timestamps
-- are UTC. The example covers 20261119-201722-995-stress-guest (50 VUs).
-- Run one test at a time; all synthetic guest IDs begin with c0dec0de-.
-- The query reconciles database attempts against their expected audit records,
-- so attempts missing BOTH events are detected too.
WITH test_window AS (
    SELECT TIMESTAMPTZ '2026-11-19 20:17:00+00' AS from_utc,
           TIMESTAMPTZ '2026-11-19 20:24:00+00' AS to_utc
), game_names(game, action_prefix) AS (
    VALUES ('grid', 'Game.Grid'), ('team-history', 'Game.TeamHistory'),
           ('missing-club', 'Game.MissingClub'), ('career-order', 'Game.CareerOrder'),
           ('mystery-player', 'Game.MysteryPlayer'), ('five-player', 'Game.StartingFive')
), attempts AS (
    SELECT 'grid'::text AS game, attempt."Completed" AS completed,
           attempt."GuestId"::text || ':grid:' || to_char(attempt."Date", 'YYYY-MM-DD') || ':' || attempt."ChallengeId"::text AS attempt_key
    FROM "DailyGridAttempts" AS attempt
    CROSS JOIN test_window AS bounds
    WHERE NOT attempt."Ranked"
      AND attempt."GuestId"::text LIKE 'c0dec0de-%'
      AND attempt."UpdatedAtUtc" >= bounds.from_utc
      AND attempt."UpdatedAtUtc" < bounds.to_utc
    UNION ALL
    SELECT attempt."Game", attempt."Completed",
           attempt."GuestId"::text || ':' || attempt."Game" || ':' || to_char(attempt."Date", 'YYYY-MM-DD') || ':' || attempt."ChallengeId"::text
    FROM "DailyGameAttempts" AS attempt
    CROSS JOIN test_window AS bounds
    WHERE attempt."UserId" IS NULL
      AND attempt."GuestId"::text LIKE 'c0dec0de-%'
      AND attempt."UpdatedAtUtc" >= bounds.from_utc
      AND attempt."UpdatedAtUtc" < bounds.to_utc
), reconciled AS (
    SELECT attempt.game, attempt.attempt_key, attempt.completed,
           count(audit."Id") FILTER (WHERE audit."Action" = names.action_prefix || 'GuestStarted') AS starts,
           count(audit."Id") FILTER (WHERE audit."Action" = names.action_prefix || 'GuestCompleted') AS completions
    FROM attempts AS attempt
    JOIN game_names AS names ON names.game = attempt.game
    LEFT JOIN "AuditLogs" AS audit
      ON audit."EntityId" = attempt.attempt_key
     AND audit."EntityType" = 'GuestGameAttempt'
     AND audit."ActorType" = 'Guest'
    GROUP BY attempt.game, attempt.attempt_key, attempt.completed
)
SELECT COALESCE(game, 'TOTAL') AS game,
       count(*) AS database_attempts,
       count(*) FILTER (WHERE completed) AS completed_attempts,
       COALESCE(sum(starts), 0) AS started_audit_events,
       COALESCE(sum(completions), 0) AS completed_audit_events,
       count(*) FILTER (WHERE starts = 0) AS missing_start_events,
       count(*) FILTER (WHERE starts > 1) AS duplicate_start_events,
       count(*) FILTER (WHERE completed AND completions = 0) AS missing_completion_events,
       count(*) FILTER (WHERE completions > 1) AS duplicate_completion_events,
       count(*) FILTER (WHERE NOT completed AND completions > 0) AS unexpected_completion_events
FROM reconciled
GROUP BY ROLLUP(game)
ORDER BY game;
