module PgMonitor.Database

open System
open System.Text
open Npgsql
open Npgsql.FSharp
open System.Collections.Generic
open System.Threading
open Models

// ── Helpers ──────────────────────────────────────────────────────────────────

/// Lift a Npgsql.FSharp Task-returning query into Async
let private run (t: Tasks.Task<'a>) = Async.AwaitTask t

// ── System / IO Metrics ──────────────────────────────────────────────────────

let getIoMetrics (cs: string) : Async<IoMetric> =
    async {
        let! row =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    sum(blks_read)::bigint   AS blks_read,
                    sum(blks_hit)::bigint    AS blks_hit,
                    sum(temp_files)::bigint  AS temp_files,
                    sum(temp_bytes)::bigint  AS temp_bytes
                FROM pg_stat_database
            """
            |> Sql.executeRowAsync (fun read ->
                let r = read.int64 "blks_read"
                let h = read.int64 "blks_hit"
                {
                    Timestamp        = DateTime.UtcNow
                    BlocksRead       = r
                    BlocksHit        = h
                    HitRatio         = if r + h = 0L then 100.0 else float h / float (r + h) * 100.0
                    TempFilesCreated = read.int64 "temp_files"
                    TempBytesWritten = read.int64 "temp_bytes"
                })
            |> run
        return row
    }

let getConnectionCount (cs: string) : Async<int * int> =
    async {
        let! row =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    (SELECT count(*) FROM pg_stat_activity)::int               AS cur,
                    (SELECT setting::int FROM pg_settings WHERE name = 'max_connections') AS max
            """
            |> Sql.executeRowAsync (fun r -> r.int "cur", r.int "max")
            |> run
        return row
    }

let getDatabaseSize (cs: string) : Async<int64> =
    async {
        let! row =
            cs
            |> Sql.connect
            |> Sql.query "SELECT pg_database_size(current_database())::bigint AS sz"
            |> Sql.executeRowAsync (fun r -> r.int64 "sz")
            |> run
        return row
    }

// ── Active Queries ───────────────────────────────────────────────────────────

let getActiveQueries (cs: string) : Async<ActiveQuery list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    pid,
                    usename,
                    datname,
                    state,
                    query,
                    COALESCE(now() - query_start, interval '0') AS duration,
                    wait_event_type,
                    wait_event,
                    client_addr::text                            AS client_addr,
                    application_name
                FROM pg_stat_activity
                WHERE state IS NOT NULL
                  AND pid <> pg_backend_pid()
                ORDER BY duration DESC
                LIMIT 200
            """
            |> Sql.executeAsync (fun r -> {
                Pid             = r.int "pid"
                Username        = r.string "usename"
                Database        = r.string "datname"
                State           = r.string "state"
                Query           = r.string "query"
                Duration        = r.fieldValue<TimeSpan> "duration"
                WaitEventType   = r.stringOrNone "wait_event_type"
                WaitEvent       = r.stringOrNone "wait_event"
                ClientAddr      = r.stringOrNone "client_addr"
                ApplicationName = r.string "application_name"
            })
            |> run
        return rows
    }

// ── pg_stat_statements ───────────────────────────────────────────────────────

let getSlowQueries (cs: string) (limit: int) : Async<SlowQuery list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query $"""
                SELECT
                    queryid,
                    query,
                    calls,
                    total_exec_time,
                    mean_exec_time,
                    stddev_exec_time,
                    min_exec_time,
                    max_exec_time,
                    rows,
                    shared_blks_hit,
                    shared_blks_read,
                    CASE WHEN shared_blks_hit + shared_blks_read = 0
                         THEN 100.0
                         ELSE 100.0 * shared_blks_hit / (shared_blks_hit + shared_blks_read)
                    END AS hit_percent
                FROM pg_stat_statements
                ORDER BY mean_exec_time DESC
                LIMIT {limit}
            """
            |> Sql.executeAsync (fun r -> {
                QueryId        = r.int64 "queryid"
                Query          = r.string "query"
                Calls          = r.int64 "calls"
                TotalTime      = r.double "total_exec_time"
                MeanTime       = r.double "mean_exec_time"
                StddevTime     = r.double "stddev_exec_time"
                MinTime        = r.double "min_exec_time"
                MaxTime        = r.double "max_exec_time"
                Rows           = r.int64 "rows"
                SharedBlksHit  = r.int64 "shared_blks_hit"
                SharedBlksRead = r.int64 "shared_blks_read"
                HitPercent     = r.double "hit_percent"
            })
            |> run
        return rows
    }

// ── EXPLAIN ANALYZE ──────────────────────────────────────────────────────────

let explainAnalyze (cs: string) (req: ExplainRequest) : Async<ExplainResponse> =
    async {
        use conn = new NpgsqlConnection(cs)
        do! conn.OpenAsync() |> Async.AwaitTask
        use cmd = conn.CreateCommand()
        cmd.CommandText    <- $"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {req.Query}"
        cmd.CommandTimeout <- 30
        use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
        let sb = StringBuilder()
        while reader.Read() do
            sb.AppendLine(reader.GetString(0)) |> ignore
        return { Plan = sb.ToString(); ExecutionTime = None }
    }

// ── Replication ──────────────────────────────────────────────────────────────

let getReplicationStatus (cs: string) : Async<ReplicationStatus> =
    async {
        let! isReplica =
            cs
            |> Sql.connect
            |> Sql.query "SELECT pg_is_in_recovery() AS r"
            |> Sql.executeRowAsync (fun r -> r.bool "r")
            |> run

        let! replicas =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    client_addr::text  AS client_addr,
                    state,
                    sent_lsn::text     AS sent_lsn,
                    write_lsn::text    AS write_lsn,
                    flush_lsn::text    AS flush_lsn,
                    replay_lsn::text   AS replay_lsn,
                    write_lag,
                    flush_lag,
                    replay_lag,
                    sync_state
                FROM pg_stat_replication
            """
            |> Sql.executeAsync (fun r -> {
                ClientAddr = r.string "client_addr"
                State      = r.string "state"
                SentLsn    = r.string "sent_lsn"
                WriteLsn   = r.string "write_lsn"
                FlushLsn   = r.string "flush_lsn"
                ReplayLsn  = r.string "replay_lsn"
                WriteLag   = r.fieldValueOrNone<TimeSpan> "write_lag"
                FlushLag   = r.fieldValueOrNone<TimeSpan> "flush_lag"
                ReplayLag  = r.fieldValueOrNone<TimeSpan> "replay_lag"
                SyncState  = r.string "sync_state"
            })
            |> run

        let! slots =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    slot_name,
                    plugin,
                    slot_type,
                    active,
                    restart_lsn::text          AS restart_lsn,
                    confirmed_flush_lsn::text  AS confirmed_flush_lsn,
                    pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn)::bigint AS lag_bytes
                FROM pg_replication_slots
            """
            |> Sql.executeAsync (fun r -> {
                SlotName          = r.string "slot_name"
                Plugin            = r.stringOrNone "plugin" |> Option.defaultValue ""
                SlotType          = r.string "slot_type"
                Active            = r.bool "active"
                RestartLsn        = r.stringOrNone "restart_lsn"
                ConfirmedFlushLsn = r.stringOrNone "confirmed_flush_lsn"
                LagBytes          = r.int64OrNone "lag_bytes"
            })
            |> run

        return {
            IsReplica   = isReplica
            PrimaryInfo = None
            Replicas    = replicas
            Slots       = slots
        }
    }

// ── Locks ────────────────────────────────────────────────────────────────────

let getLocks (cs: string) : Async<LockInfo list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    l.pid,
                    a.usename,
                    a.query,
                    l.locktype,
                    c.relname,
                    l.mode,
                    l.granted,
                    COALESCE(now() - a.query_start, interval '0') AS duration
                FROM pg_locks l
                JOIN pg_stat_activity a ON a.pid = l.pid
                LEFT JOIN pg_class c    ON c.oid  = l.relation
                WHERE a.pid <> pg_backend_pid()
                ORDER BY duration DESC
            """
            |> Sql.executeAsync (fun r -> {
                Pid        = r.int "pid"
                Username   = r.string "usename"
                Query      = r.string "query"
                LockType   = r.string "locktype"
                Relation   = r.stringOrNone "relname"
                Mode       = r.string "mode"
                Granted    = r.bool "granted"
                WaitingFor = None
                Duration   = r.fieldValue<TimeSpan> "duration"
            })
            |> run
        return rows
    }

// ── Vacuum ───────────────────────────────────────────────────────────────────

let getVacuumActivity (cs: string) : Async<VacuumActivity list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    pid,
                    datname,
                    phase,
                    relid::regclass::text AS relname,
                    heap_blks_total,
                    heap_blks_scanned,
                    heap_blks_vacuumed,
                    index_vacuum_count
                    /* , max_dead_tuples*/
                    /* , num_dead_tuples */
                FROM pg_stat_progress_vacuum
            """
            |> Sql.executeAsync (fun r ->
                let rel   = r.string "relname"
                let parts = rel.Split('.')
                let schema, tbl =
                    if parts.Length = 2 then parts.[0], parts.[1] else "public", rel
                {
                    Pid              = r.int "pid"
                    Database         = r.string "datname"
                    Schema           = schema
                    Table            = tbl
                    Phase            = r.string "phase"
                    HeapBlksTotal    = r.int64 "heap_blks_total"
                    HeapBlksScanned  = r.int64 "heap_blks_scanned"
                    HeapBlksvacuumed = r.int64 "heap_blks_vacuumed"
                    IndexVacuumCount = r.int64 "index_vacuum_count"
                    MaxDeadTuples    = -1 // r.int64 "max_dead_tuples"
                    NumDeadTuples    = -1 // r.int64 "num_dead_tuples"
                })
            |> run
        return rows
    }

let getTableBloat (cs: string) : Async<TableBloat list> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT
                    schemaname                                AS schema,
                    relname                                   AS table_name,
                    n_live_tup                                AS live_tuples,
                    n_dead_tup                                AS dead_tuples,
                    last_vacuum,
                    last_autovacuum,
                    last_analyze,
                    last_autoanalyze,
                    vacuum_count,
                    CASE WHEN n_live_tup + n_dead_tup = 0 THEN 0.0
                         ELSE 100.0 * n_dead_tup / (n_live_tup + n_dead_tup)
                    END                                       AS bloat_pct,
                    pg_total_relation_size(relid)::bigint     AS size_bytes
                FROM pg_stat_user_tables
                ORDER BY n_dead_tup DESC
                LIMIT 50
            """
            |> Sql.executeAsync (fun r -> {
                Schema               = r.string "schema"
                TableName            = r.string "table_name"
                LiveTuples           = r.int64 "live_tuples"
                DeadTuples           = r.int64 "dead_tuples"
                LastVacuum           = r.datetimeOffsetOrNone "last_vacuum"      |> Option.map (fun d -> d.UtcDateTime)
                LastAutoVacuum       = r.datetimeOffsetOrNone "last_autovacuum"  |> Option.map (fun d -> d.UtcDateTime)
                LastAnalyze          = r.datetimeOffsetOrNone "last_analyze"     |> Option.map (fun d -> d.UtcDateTime)
                LastAutoAnalyze      = r.datetimeOffsetOrNone "last_autoanalyze" |> Option.map (fun d -> d.UtcDateTime)
                VacuumCount          = r.int64 "vacuum_count"
                BloatEstimatePercent = r.double "bloat_pct"
                SizeBytes            = r.int64 "size_bytes"
            })
            |> run
        return rows
    }

// ── SQL Runner ────────────────────────────────────────────────────────────────

let runSql (cs: string) (sql: string) : Async<Models.SqlRunResult> =
    async {
        let sw = System.Diagnostics.Stopwatch.StartNew()
        try
            use conn = new NpgsqlConnection(cs)
            do! conn.OpenAsync() |> Async.AwaitTask
            use cmd = conn.CreateCommand()
            cmd.CommandText    <- sql
            cmd.CommandTimeout <- 30
            use! reader = cmd.ExecuteReaderAsync() |> Async.AwaitTask
            let cols =
                [ for i in 0 .. reader.FieldCount - 1 -> reader.GetName(i) ]
            let rows = List<string list>()
            while reader.Read() do
                let row = [ for i in 0 .. reader.FieldCount - 1 ->
                                if reader.IsDBNull(i) then "NULL"
                                else string (reader.GetValue(i)) ]
                rows.Add(row)
            sw.Stop()
            return {
                Columns         = cols
                Rows            = rows |> Seq.toList
                RowCount        = rows.Count
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                Error           = None
            }
        with ex ->
            sw.Stop()
            return {
                Columns         = []
                Rows            = []
                RowCount        = 0
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
                Error           = Some ex.Message
            }
    }
