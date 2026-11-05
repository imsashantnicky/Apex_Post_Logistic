/* A third session, run while B is blocked: who waits for whom, on what, and which locks exist. */
SET NOCOUNT ON;
SELECT waiting.session_id AS Waiting, waiting.blocking_session_id AS BlockedBy, waiting.wait_type AS WaitType,
       waiting.wait_duration_ms AS WaitMs, waiting.resource_description AS Resource
FROM sys.dm_os_waiting_tasks AS waiting
WHERE waiting.blocking_session_id IS NOT NULL
  AND waiting.session_id <> waiting.blocking_session_id;

SELECT locks.request_session_id AS Session, locks.resource_type AS Resource, locks.request_mode AS Mode,
       locks.request_status AS Status
FROM sys.dm_tran_locks AS locks
WHERE locks.resource_database_id = DB_ID()
  AND locks.resource_type IN ('KEY', 'PAGE', 'OBJECT')
  AND locks.request_session_id <> @@SPID
ORDER BY locks.request_session_id, locks.resource_type;
