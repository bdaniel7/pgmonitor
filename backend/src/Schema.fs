module PgMonitor.Schema

open System
open Npgsql.FSharp

/// Creates all pgm_* tables and seeds default users if none exist.
/// Call once at startup before anything else uses the DB.
let init (cs: string) : Async<unit> =
    async {
        // ── Tables ────────────────────────────────────────────────────────────
        let ddl = """
            CREATE TABLE IF NOT EXISTS public.pgm_users (
                id            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                username      TEXT        UNIQUE NOT NULL,
                password_hash TEXT        NOT NULL,
                role          TEXT        NOT NULL DEFAULT 'viewer',
                created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS public.pgm_connections (
                id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                name       TEXT        NOT NULL,
                host       TEXT        NOT NULL,
                port       INT         NOT NULL DEFAULT 5432,
                database   TEXT        NOT NULL,
                username   TEXT        NOT NULL,
                password   TEXT        NOT NULL,
                is_primary BOOLEAN     NOT NULL DEFAULT false,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS public.pgm_alert_rules (
                id        UUID    PRIMARY KEY,
                name      TEXT    NOT NULL,
                metric    TEXT    NOT NULL,
                threshold FLOAT8  NOT NULL,
                severity  TEXT    NOT NULL,
                enabled   BOOLEAN NOT NULL DEFAULT true
            );

            CREATE TABLE IF NOT EXISTS public.pgm_alerts (
                id           UUID        PRIMARY KEY,
                rule_id      UUID        NOT NULL REFERENCES pgm_alert_rules(id) ON DELETE CASCADE,
                rule_name    TEXT        NOT NULL,
                severity     TEXT        NOT NULL,
                message      TEXT        NOT NULL,
                value        FLOAT8      NOT NULL,
                threshold    FLOAT8      NOT NULL,
                triggered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                resolved_at  TIMESTAMPTZ,
                acknowledged BOOLEAN     NOT NULL DEFAULT false
            );
        """

        use conn = new Npgsql.NpgsqlConnection(cs)
        do! conn.OpenAsync() |> Async.AwaitTask
        use cmd  = conn.CreateCommand()
        cmd.CommandText <- ddl
        let! _ = cmd.ExecuteNonQueryAsync() |> Async.AwaitTask
        ()

        // ── Seed default users if the table is empty ──────────────────────────
        let! count =
            cs
            |> Sql.connect
            |> Sql.query "SELECT count(*)::int AS n FROM pgm_users"
            |> Sql.executeRowAsync (fun r -> r.int "n")
            |> Async.AwaitTask

        if count = 0 then
            use conn2 = new Npgsql.NpgsqlConnection(cs)
            do! conn2.OpenAsync() |> Async.AwaitTask
            use cmd2 = conn2.CreateCommand()
            cmd2.CommandText <- """
                INSERT INTO pgm_users (username, password_hash, role) VALUES
                    ('admin',  @adminHash,  'admin'),
                    ('viewer', @viewerHash, 'viewer')
            """
            cmd2.Parameters.AddWithValue("adminHash",  BCrypt.Net.BCrypt.HashPassword("admin123"))  |> ignore
            cmd2.Parameters.AddWithValue("viewerHash", BCrypt.Net.BCrypt.HashPassword("viewer123")) |> ignore
            let! _ = cmd2.ExecuteNonQueryAsync() |> Async.AwaitTask
            ()
    }
