module PgMonitor.Handlers

open System
open Microsoft.AspNetCore.Http
open System.Threading.Tasks
open Models
open Database
open Alerts
open Auth
open ConnectionStore

// ── Auth ──────────────────────────────────────────────────────────────────────

let loginHandler (cs: string) (secret: string) : Func<LoginRequest, Task<IResult>> =
    Func<LoginRequest, Task<IResult>>(fun req -> task {
        let! ok = validateCredentials cs req.Username req.Password
        if ok then
            let! token = generateToken cs req.Username secret
            return Results.Ok(token)
        else
            return Results.Unauthorized()
    })

// ── User management (admin only) ──────────────────────────────────────────────

let listUsersHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! users = listUsers cs
        return Results.Ok(users)
    })

let createUserHandler (cs: string) : Func<CreateUserRequest, Task<IResult>> =
    Func<CreateUserRequest, Task<IResult>>(fun req -> task {
        try
            let! user = createUser cs req
            return Results.Created("/api/users", user)
        with ex ->
            return Results.BadRequest({| error = ex.Message |})
    })

let deleteUserHandler (cs: string) : Func<Guid, Task<IResult>> =
    Func<Guid, Task<IResult>>(fun id -> task {
        do! deleteUser cs id
        return Results.NoContent()
    })

let changePasswordHandler (cs: string) : Func<Guid, ChangePasswordRequest, Task<IResult>> =
    Func<Guid, ChangePasswordRequest, Task<IResult>>(fun id req -> task {
        do! changePassword cs id req.NewPassword
        return Results.NoContent()
    })

// ── Connection management ─────────────────────────────────────────────────────

let listConnectionsHandler () : Func<IResult> =
    Func<IResult>(fun () ->
        // Strip passwords before returning
        let safe =
            getAll()
            |> List.map (fun c -> {| c with Password = "***" |})
        Results.Ok(safe))

let addConnectionHandler (cs: string) : Func<CreateConnectionRequest, Task<IResult>> =
    Func<CreateConnectionRequest, Task<IResult>>(fun req -> task {
        try
            let! conn = add cs req
            return Results.Created("/api/connections", {| conn with Password = "***" |})
        with ex ->
            return Results.BadRequest({| error = ex.Message |})
    })

let deleteConnectionHandler (cs: string) : Func<Guid, Task<IResult>> =
    Func<Guid, Task<IResult>>(fun id -> task {
        do! delete cs id
        return Results.NoContent()
    })

let testConnectionHandler () : Func<CreateConnectionRequest, Task<IResult>> =
    Func<CreateConnectionRequest, Task<IResult>>(fun req -> task {
        let! ok = testConnection req
        return Results.Ok({| ok = ok |})
    })

// ── Metrics ───────────────────────────────────────────────────────────────────

let snapshotHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! io            = getIoMetrics cs      |> Async.StartAsTask
        let! connPair      = getConnectionCount cs |> Async.StartAsTask
        let! size          = getDatabaseSize cs    |> Async.StartAsTask
        let  cpu           = Metrics.getCpuMetric()
        let  disks         = Metrics.getDiskMetrics()
        let  conn, maxConn = connPair
        Alerts.checkMetric cs "cpu_percent"    cpu.UsagePercent                          |> ignore
        Alerts.checkMetric cs "cache_hit_ratio" io.HitRatio                              |> ignore
        Alerts.checkMetric cs "connection_pct" (float conn / float maxConn * 100.0)      |> ignore
        return Results.Ok({
            Cpu = cpu; Disks = disks; Io = io
            Connections = conn; MaxConnections = maxConn; DatabaseSize = size
        })
    })

let activeQueriesHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! queries = getActiveQueries cs |> Async.StartAsTask
        queries |> List.iter (fun q ->
            Alerts.checkMetric cs "query_duration_s" q.Duration.TotalSeconds |> ignore)
        return Results.Ok(queries)
    })

let slowQueriesHandler (cs: string) : Func<int, Task<IResult>> =
    Func<int, Task<IResult>>(fun limit -> task {
        let! queries = getSlowQueries cs limit |> Async.StartAsTask
        return Results.Ok(queries)
    })

let explainHandler (cs: string) : Func<ExplainRequest, Task<IResult>> =
    Func<ExplainRequest, Task<IResult>>(fun req -> task {
        try
            let! result = explainAnalyze cs req |> Async.StartAsTask
            return Results.Ok(result)
        with ex ->
            return Results.BadRequest({| error = ex.Message |})
    })

// ── SQL Runner ────────────────────────────────────────────────────────────────

let sqlRunHandler (primaryCs: string) : Func<SqlRunRequest, Task<IResult>> =
    Func<SqlRunRequest, Task<IResult>>(fun req -> task {
        let targetCs = ConnectionStore.resolve primaryCs req.ConnectionId
        let! result  = runSql targetCs req.Sql |> Async.StartAsTask
        return Results.Ok(result)
    })

// ── Replication ───────────────────────────────────────────────────────────────

let replicationHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! status = getReplicationStatus cs |> Async.StartAsTask
        return Results.Ok(status)
    })

// ── Locks ─────────────────────────────────────────────────────────────────────

let locksHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! locks = getLocks cs |> Async.StartAsTask
        return Results.Ok(locks)
    })

// ── Vacuum ────────────────────────────────────────────────────────────────────

let vacuumActivityHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! activity = getVacuumActivity cs |> Async.StartAsTask
        return Results.Ok(activity)
    })

let tableBloatHandler (cs: string) : Func<Task<IResult>> =
    Func<Task<IResult>>(fun () -> task {
        let! bloat = getTableBloat cs |> Async.StartAsTask
        return Results.Ok(bloat)
    })

// ── Alerts ────────────────────────────────────────────────────────────────────

let alertRulesHandler () : Func<IResult> =
    Func<IResult>(fun () -> Results.Ok(getRules()))

let alertsHandler () : Func<IResult> =
    Func<IResult>(fun () -> Results.Ok(getAlerts()))

let addAlertRuleHandler (cs: string) : Func<AlertRule, Task<IResult>> =
    Func<AlertRule, Task<IResult>>(fun rule -> task {
        let r = { rule with Id = Guid.NewGuid() }
        do! addRule cs r
        return Results.Created("/api/alerts/rules", r)
    })

let deleteAlertRuleHandler (cs: string) : Func<Guid, Task<IResult>> =
    Func<Guid, Task<IResult>>(fun id -> task {
        do! deleteRule cs id
        return Results.NoContent()
    })

let acknowledgeAlertHandler (cs: string) : Func<Guid, Task<IResult>> =
    Func<Guid, Task<IResult>>(fun id -> task {
        do! acknowledgeAlert cs id
        return Results.Ok()
    })
