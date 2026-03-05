module PgMonitor.ConnectionStore

open System
open Npgsql.FSharp
open Models

let mutable private connections : DbConnection list = []

// ── Load from DB ──────────────────────────────────────────────────────────────

let load (cs: string) : Async<unit> =
    async {
        let! rows =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT id, name, host, port, database, username, password, is_primary
                FROM pgm_connections
                ORDER BY created_at
            """
            |> Sql.executeAsync (fun r -> {
                Id         = r.uuid "id"
                Name       = r.string "name"
                Host       = r.string "host"
                Port       = r.int "port"
                Database   = r.string "database"
                Username   = r.string "username"
                Password   = r.string "password"
                IsPrimary  = r.bool "is_primary"
            })
            |> Async.AwaitTask
        connections <- rows
    }

// ── Query ─────────────────────────────────────────────────────────────────────

let getAll () = connections

/// Returns the connection string for the given id, falling back to primaryCs.
let resolve (primaryCs: string) (id: Guid option) : string =
    match id with
    | None -> primaryCs
    | Some gid ->
        connections
        |> List.tryFind (fun c -> c.Id = gid)
        |> Option.map (fun c ->
            $"Host={c.Host};Port={c.Port};Database={c.Database};Username={c.Username};Password={c.Password}")
        |> Option.defaultValue primaryCs

// ── CRUD ──────────────────────────────────────────────────────────────────────

let add (cs: string) (req: CreateConnectionRequest) : Async<DbConnection> =
    async {
        let id = Guid.NewGuid()
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query """
                INSERT INTO pgm_connections (id, name, host, port, database, username, password)
                VALUES (@id, @name, @host, @port, @db, @user, @pass)
            """
            |> Sql.parameters [
                "id",   Sql.uuid id
                "name", Sql.string req.Name
                "host", Sql.string req.Host
                "port", Sql.int req.Port
                "db",   Sql.string req.Database
                "user", Sql.string req.Username
                "pass", Sql.string req.Password
            ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        let conn = {
            Id = id; Name = req.Name; Host = req.Host; Port = req.Port
            Database = req.Database; Username = req.Username; Password = req.Password
            IsPrimary = false
        }
        connections <- connections @ [conn]
        return conn
    }

let delete (cs: string) (id: Guid) : Async<unit> =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query "DELETE FROM pgm_connections WHERE id = @id AND is_primary = false"
            |> Sql.parameters ["id", Sql.uuid id]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        connections <- connections |> List.filter (fun c -> c.Id <> id)
    }

let testConnection (req: CreateConnectionRequest) : Async<bool> =
    async {
        let testCs = $"Host={req.Host};Port={req.Port};Database={req.Database};Username={req.Username};Password={req.Password}"
        try
            use conn = new Npgsql.NpgsqlConnection(testCs)
            do! conn.OpenAsync() |> Async.AwaitTask
            return true
        with _ ->
            return false
    }
