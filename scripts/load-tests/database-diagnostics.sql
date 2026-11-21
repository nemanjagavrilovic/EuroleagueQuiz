-- Run each SELECT separately in pgAdmin Query Tool or Supabase SQL Editor,
-- connected to the SAME test database as the local API. All statements are read-only.
-- Refresh live checks every 5-10 seconds DURING a test, with autocommit on.
-- Do not leave these queries inside a long-running transaction.

-- 1. Connection usage. max_connections is the Postgres SERVER limit;
-- Supabase pooler limits and the application's Npgsql pool are separate limits.
SELECT clock_timestamp() AS sampled_at,
       current_database() AS database_name,
       current_setting('max_connections')::int AS server_max_connections,
       count(*) FILTER (WHERE backend_type = 'client backend') AS server_client_connections,
       count(*) FILTER (WHERE backend_type = 'client backend' AND datname = current_database()) AS database_client_connections,
       count(*) FILTER (WHERE datname = current_database() AND state = 'active' AND pid <> pg_backend_pid()) AS database_active,
       count(*) FILTER (WHERE datname = current_database() AND state = 'idle') AS database_idle,
       count(*) FILTER (WHERE datname = current_database() AND state LIKE 'idle in transaction%') AS idle_in_transaction
FROM pg_stat_activity;

-- 2. Sessions executing or waiting now. Query duration includes waiting.
-- Long transaction age plus idle-in-transaction state deserves investigation.
SELECT pid, application_name, state, wait_event_type, wait_event,
       clock_timestamp() - query_start AS query_age,
       clock_timestamp() - xact_start AS transaction_age
FROM pg_stat_activity
WHERE datname = current_database()
  AND pid <> pg_backend_pid()
  AND (state = 'active' OR state LIKE 'idle in transaction%')
ORDER BY query_start;

-- 3. Blocked sessions and their blockers, including advisory-lock waits.
-- No rows means no blocking observed AT THIS INSTANT.
SELECT waiting.pid AS waiting_pid, blocker.blocking_pid,
       waiting.application_name, waiting.wait_event_type, waiting.wait_event,
       clock_timestamp() - waiting.query_start AS waiting_query_age,
       holder.state AS blocker_state,
       clock_timestamp() - holder.xact_start AS blocker_transaction_age
FROM pg_stat_activity AS waiting
CROSS JOIN LATERAL unnest(pg_blocking_pids(waiting.pid)) AS blocker(blocking_pid)
LEFT JOIN pg_stat_activity AS holder ON holder.pid = blocker.blocking_pid
WHERE waiting.datname = current_database()
  AND waiting.pid <> pg_backend_pid()
ORDER BY waiting.query_start;

-- 4. Discover optional query statistics support. No rows means it is not installed.
-- This file NEVER installs extensions or resets counters.
SELECT extension.extname, namespace.nspname AS extension_schema
FROM pg_extension AS extension
JOIN pg_namespace AS namespace ON namespace.oid = extension.extnamespace
WHERE extension.extname = 'pg_stat_statements';

-- OPTIONAL, only if check 4 returns a row:
-- Use <extension_schema>.pg_stat_statements if the unqualified view is not found.
-- Export before and after a test. Counters are cumulative since their last reset;
-- compare delta(calls), delta(total_exec_time), and total-time delta / calls delta.
-- Normalized query text can still contain literals; review it before sharing.
-- SELECT queryid, calls,
--        round(total_exec_time::numeric, 2) AS total_exec_ms,
--        round(mean_exec_time::numeric, 2) AS mean_exec_ms,
--        round(max_exec_time::numeric, 2) AS max_exec_ms,
--        rows, left(query, 500) AS query_text
-- FROM pg_stat_statements
-- WHERE dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
-- ORDER BY total_exec_time DESC
-- LIMIT 20;
