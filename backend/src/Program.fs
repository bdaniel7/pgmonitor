module PgMonitor.Program

open System
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Microsoft.AspNetCore.SignalR
open Serilog
open Serilog.Context
open Hubs
open Handlers
open PgMonitor.Models


// ── Correlation-ID middleware ─────────────────────────────────────────────────
// Reads X-Correlation-Id from the incoming request (or mints a new UUID),
// echoes it back in the response, and pushes it into Serilog's LogContext so
// every log event emitted during the request carries the same CorrelationId.

let private correlationMiddleware (ctx: HttpContext) (next: Func<Task>)=
    task {
        let id =
            match ctx.Request.Headers.TryGetValue("X-Correlation-Id") with
            | true, v when v.Count > 0 -> v.[0]
            | _ -> Guid.NewGuid().ToString("N")
        ctx.Response.Headers["X-Correlation-Id"] <- id
        use _ = LogContext.PushProperty("CorrelationId", id)
        do! next.Invoke()
    } :> Task

[<EntryPoint>]
let main argv =

    // ── Serilog bootstrap (before builder so startup errors are captured) ─────
    Log.Logger <-
        LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger()

    try

        let builder = WebApplication.CreateBuilder(argv)

        // ── Config ────────────────────────────────────────────────────────────────
        let cfg    = builder.Configuration
        let cs     = match cfg["ConnectionStrings:Postgres"] with
                            | null -> raise (ApplicationException("ConnectionStrings:Postgres is missing from appsettings.json!"))
                            | v -> v

        let jwtKey = match cfg["Jwt:Key"] with
                            | null -> raise (ApplicationException("Jwt:Key is missing from appsettings.json!"))
                            | v -> v

        let seqUrl = match cfg["Seq:Url"] with
                           | null -> "http://localhost:5341"
                           | v -> v

        let corsOrigins = match cfg["App:CorsOrigins"] with
                               | null -> [|"http://localhost:5173"; "http://localhost:4173"|]
                               | v -> v.Split(",")

        // ── Serilog full configuration ────────────────────────────────────────
        builder.Host.UseSerilog(fun ctx _ logCfg ->
            logCfg
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(
                    outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId,-36} {Message:lj}{NewLine}{Exception}")
                .WriteTo.Seq(seqUrl,
                    apiKey = null,
                    period = TimeSpan.FromSeconds(2.0))
            |> ignore
        ) |> ignore

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
                p.WithOrigins(corsOrigins)
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials() |> ignore)
        ) |> ignore

        let app = builder.Build()


         // ── Middleware pipeline ───────────────────────────────────────────────
        app.Use(fun ctx next -> correlationMiddleware ctx next) |> ignore

        app.UseSerilogRequestLogging(fun opts ->
            opts.MessageTemplate <- "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0}ms [{CorrelationId}]"
        ) |> ignore

        app.UseCors()           |> ignore
        app.UseAuthentication() |> ignore
        app.UseAuthorization()  |> ignore

        // ── DB init ───────────────────────────────────────────────────────────────
        Log.Information("Initialising database schema…")
        Schema.init cs |> Async.RunSynchronously
        Log.Information("Loading connections and alert rules…")
        ConnectionStore.load cs |> Async.RunSynchronously
        Alerts.load cs |> Async.RunSynchronously
        Log.Information("Startup complete. Seq sink → {SeqUrl}", seqUrl)

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

                    if not newAlerts.IsEmpty then
                            for a in newAlerts do
                                Log.Warning("Alert fired: {RuleName} value={Value} threshold={Threshold} severity={Severity}",
                                    a.RuleName, a.Value, a.Threshold, a.Severity)
                            do! hub.Clients.All.SendAsync("alerts", newAlerts)

                    do! hub.Clients.All.SendAsync("snapshot",      snap)
                    do! hub.Clients.All.SendAsync("activeQueries", queries)
                    if not newAlerts.IsEmpty then
                        do! hub.Clients.All.SendAsync("alerts", newAlerts)
                with ex ->
                    Log.Error(ex, "Broadcast loop error")
                do! Task.Delay(3000)
        }

        broadcastLoop() |> ignore

        app.Run()
        0
    with ex ->
        Log.Fatal(ex, "Application terminated unexpectedly")
        1
    |> fun code ->
        Log.CloseAndFlush()
        code
