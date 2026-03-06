module PgMonitor.Program

open System
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.SignalR
open Hubs
open Handlers
open PgMonitor.Models

[<EntryPoint>]
let main argv =
    let builder = WebApplication.CreateBuilder(argv)

    // ── Config ────────────────────────────────────────────────────────────────
    let cfg    = builder.Configuration
    let cs     = match cfg["ConnectionStrings:Postgres"] with
                            | null -> "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=6EaL64EkXfGDmm5wZCE0"
                            | v -> v

    let jwtKey = match cfg["Jwt:Key"] with
                            | null -> "super-secret-key-change-in-production-32chars!!"
                            | v -> v

    // ── JSON ──────────────────────────────────────────────────────────────────
    builder.Services.ConfigureHttpJsonOptions(fun opts ->
        opts.SerializerOptions.Converters.Add(JsonStringEnumConverter())
        opts.SerializerOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    ) |> ignore

    builder.Services.Configure<JsonOptions> (fun (opts: JsonOptions) ->
        opts.SerializerOptions.Converters.Add(AlertSeverityConverter())) |> ignore

    // ── JWT Auth ──────────────────────────────────────────────────────────────
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun opts ->
            opts.TokenValidationParameters <- TokenValidationParameters(
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = "pgmonitor",
                ValidAudience            = "pgmonitor",
                IssuerSigningKey         = SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            )
            // Allow token in query string for SignalR WebSocket connections
            opts.Events <- JwtBearerEvents(
                OnMessageReceived = fun ctx ->
                    let token = ctx.Request.Query["access_token"]
                    let path  = ctx.HttpContext.Request.Path
                    if token.Count > 0 && path.StartsWithSegments("/hubs") then
                        ctx.Token <- token[0]
                    Task.CompletedTask
            )
        ) |> ignore

    builder.Services.AddAuthorization(fun opts ->
        opts.AddPolicy("AdminOnly", fun p -> p.RequireRole("admin") |> ignore)
    ) |> ignore

    // ── SignalR ───────────────────────────────────────────────────────────────
    builder.Services.AddSignalR() |> ignore

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(fun o ->
        o.AddDefaultPolicy(fun p ->
            p.WithOrigins("http://localhost:5173", "http://localhost:4173")
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials() |> ignore)
    ) |> ignore

    let app = builder.Build()

    app.UseCors()           |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization()  |> ignore

    // ── DB init ───────────────────────────────────────────────────────────────
    Schema.init cs |> Async.RunSynchronously
    ConnectionStore.load cs |> Async.RunSynchronously
    Alerts.load cs |> Async.RunSynchronously

    // ── Routes ────────────────────────────────────────────────────────────────
    let api   = app.MapGroup("/api")
    let auth  = api.MapGroup("").RequireAuthorization()
    let admin = api.MapGroup("").RequireAuthorization("AdminOnly")

    // Public
    api.MapPost("/auth/login", loginHandler cs jwtKey) |> ignore

    // Any authenticated user
    auth.MapGet("/snapshot",                   snapshotHandler cs)        |> ignore
    auth.MapGet("/queries/active",             activeQueriesHandler cs)   |> ignore
    auth.MapGet("/queries/slow",               slowQueriesHandler cs)     |> ignore
    auth.MapPost("/queries/explain",           explainHandler cs)         |> ignore
    auth.MapGet("/replication",                replicationHandler cs)     |> ignore
    auth.MapGet("/locks",                      locksHandler cs)           |> ignore
    auth.MapGet("/vacuum/activity",            vacuumActivityHandler cs)  |> ignore
    auth.MapGet("/vacuum/bloat",               tableBloatHandler cs)      |> ignore
    auth.MapGet("/alerts",                     alertsHandler ())          |> ignore
    auth.MapGet("/alerts/rules",               alertRulesHandler ())      |> ignore
    auth.MapPost("/alerts/{id}/acknowledge",   acknowledgeAlertHandler cs)|> ignore
    auth.MapGet("/connections",                listConnectionsHandler ()) |> ignore
    auth.MapPost("/sql/run",                   sqlRunHandler cs)          |> ignore

    // Admin only
    admin.MapPost("/alerts/rules",             addAlertRuleHandler cs)    |> ignore
    admin.MapDelete("/alerts/rules/{id}",      deleteAlertRuleHandler cs) |> ignore
    admin.MapGet("/users",                     listUsersHandler cs)       |> ignore
    admin.MapPost("/users",                    createUserHandler cs)      |> ignore
    admin.MapDelete("/users/{id}",             deleteUserHandler cs)      |> ignore
    admin.MapPatch("/users/{id}/password",     changePasswordHandler cs)  |> ignore
    admin.MapPost("/connections",              addConnectionHandler cs)   |> ignore
    admin.MapDelete("/connections/{id}",       deleteConnectionHandler cs)|> ignore
    admin.MapPost("/connections/test",         testConnectionHandler ())  |> ignore

    // SignalR hub
    app.MapHub<MonitorHub>("/hubs/monitor") |> ignore

    // ── Background broadcast loop ─────────────────────────────────────────────
    let hub = app.Services.GetRequiredService<IHubContext<MonitorHub>>()

    let broadcastLoop () = task {
        while true do
            try
                let! io       = Database.getIoMetrics cs      |> Async.StartAsTask
                let! connPair = Database.getConnectionCount cs |> Async.StartAsTask
                let! size     = Database.getDatabaseSize cs    |> Async.StartAsTask
                let  cpu      = Metrics.getCpuMetric()
                let  disks    = Metrics.getDiskMetrics()
                let  conn, maxC = connPair
                let  snap = {
                    Models.Cpu = cpu; Models.Disks = disks; Models.Io = io
                    Models.Connections = conn; Models.MaxConnections = maxC
                    Models.DatabaseSize = size
                }
                let! queries   = Database.getActiveQueries cs |> Async.StartAsTask
                let  newAlerts = Alerts.checkMetric cs "cpu_percent" cpu.UsagePercent
                do! hub.Clients.All.SendAsync("snapshot",      snap)
                do! hub.Clients.All.SendAsync("activeQueries", queries)
                if not newAlerts.IsEmpty then
                    do! hub.Clients.All.SendAsync("alerts", newAlerts)
            with ex ->
                eprintfn "[broadcast] %s" ex.Message
            do! System.Threading.Tasks.Task.Delay(3000)
    }

    broadcastLoop() |> ignore

    app.Run()
    0
