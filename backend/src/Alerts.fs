module PgMonitor.Alerts

open System
open Npgsql.FSharp
open Models

// ── In-memory cache (persisted to pgm_alert_rules / pgm_alerts) ───────────────

let mutable private rules  : AlertRule list = []
let mutable private alerts : Alert list     = []

// ── Persistence helpers ───────────────────────────────────────────────────────

let private severityToStr = function
    | Info     -> "Info"
    | Warning  -> "Warning"
    | Critical -> "Critical"

let private strToSeverity = function
    | "Critical" -> Critical
    | "Warning"  -> Warning
    | _          -> Info

/// Load rules and open alerts from the database into memory.
let load (cs: string) : Async<unit> =
    async {
        let! dbRules =
            cs
            |> Sql.connect
            |> Sql.query "SELECT id, name, metric, threshold, severity, enabled FROM pgm_alert_rules"
            |> Sql.executeAsync (fun r -> {
                Id        = r.uuid "id"
                Name      = r.string "name"
                Metric    = r.string "metric"
                Threshold = r.double "threshold"
                Severity  = r.string "severity" |> strToSeverity
                Enabled   = r.bool "enabled"
            })
            |> Async.AwaitTask

        let! dbAlerts =
            cs
            |> Sql.connect
            |> Sql.query """
                SELECT id, rule_id, rule_name, severity, message, value,
                       threshold, triggered_at, resolved_at, acknowledged
                FROM pgm_alerts
                ORDER BY triggered_at DESC
                LIMIT 500
            """
            |> Sql.executeAsync (fun r -> {
                Id           = r.uuid "id"
                RuleId       = r.uuid "rule_id"
                RuleName     = r.string "rule_name"
                Severity     = r.string "severity" |> strToSeverity
                Message      = r.string "message"
                Value        = r.double "value"
                Threshold    = r.double "threshold"
                TriggeredAt  = r.datetimeOffset "triggered_at" |> fun d -> d.UtcDateTime
                ResolvedAt   = r.datetimeOffsetOrNone "resolved_at" |> Option.map (fun d -> d.UtcDateTime)
                Acknowledged = r.bool "acknowledged"
            })
            |> Async.AwaitTask

        // If no rules in DB, seed defaults
        if dbRules.IsEmpty then
            rules <- [
                { Id = Guid.NewGuid(); Name = "High CPU";            Metric = "cpu_percent";       Threshold = 85.0;        Severity = Warning;  Enabled = true }
                { Id = Guid.NewGuid(); Name = "Critical CPU";        Metric = "cpu_percent";       Threshold = 95.0;        Severity = Critical; Enabled = true }
                { Id = Guid.NewGuid(); Name = "Low Cache Hit Ratio"; Metric = "cache_hit_ratio";   Threshold = 90.0;        Severity = Warning;  Enabled = true }
                { Id = Guid.NewGuid(); Name = "Long-Running Query";  Metric = "query_duration_s";  Threshold = 30.0;        Severity = Warning;  Enabled = true }
                { Id = Guid.NewGuid(); Name = "Too Many Connections";Metric = "connection_pct";    Threshold = 80.0;        Severity = Warning;  Enabled = true }
                { Id = Guid.NewGuid(); Name = "Replication Lag";     Metric = "repl_lag_bytes";    Threshold = 104857600.0; Severity = Critical; Enabled = true }
            ]
            for rule in rules do
                let! _ =
                    cs
                    |> Sql.connect
                    |> Sql.query """
                        INSERT INTO pgm_alert_rules (id, name, metric, threshold, severity, enabled)
                        VALUES (@id, @n, @m, @t, @s, @e)
                    """
                    |> Sql.parameters [
                        "id", Sql.uuid rule.Id
                        "n",  Sql.string rule.Name
                        "m",  Sql.string rule.Metric
                        "t",  Sql.double rule.Threshold
                        "s",  Sql.string (severityToStr rule.Severity)
                        "e",  Sql.bool rule.Enabled
                    ]
                    |> Sql.executeNonQueryAsync
                    |> Async.AwaitTask
                ()
        else
            rules <- dbRules

        alerts <- dbAlerts
    }

// ── Public accessors ──────────────────────────────────────────────────────────

let getRules () = rules
let getAlerts () = alerts |> List.sortByDescending (fun a -> a.TriggeredAt)

// ── Rule CRUD ─────────────────────────────────────────────────────────────────

let addRule (cs: string) (rule: AlertRule) : Async<unit> =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query """
                INSERT INTO pgm_alert_rules (id, name, metric, threshold, severity, enabled)
                VALUES (@id, @n, @m, @t, @s, @e)
            """
            |> Sql.parameters [
                "id", Sql.uuid rule.Id
                "n",  Sql.string rule.Name
                "m",  Sql.string rule.Metric
                "t",  Sql.double rule.Threshold
                "s",  Sql.string (severityToStr rule.Severity)
                "e",  Sql.bool rule.Enabled
            ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        rules <- rule :: rules
    }

let deleteRule (cs: string) (id: Guid) : Async<unit> =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query "DELETE FROM pgm_alert_rules WHERE id = @id"
            |> Sql.parameters ["id", Sql.uuid id]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        rules  <- rules  |> List.filter (fun r -> r.Id <> id)
        alerts <- alerts |> List.filter (fun a -> a.RuleId <> id)
    }

// ── Alert CRUD ────────────────────────────────────────────────────────────────

let private persistAlert (cs: string) (a: Alert) : unit =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query """
                INSERT INTO pgm_alerts (id, rule_id, rule_name, severity, message, value, threshold, triggered_at)
                VALUES (@id, @rid, @rn, @sev, @msg, @v, @t, @ta)
                ON CONFLICT (id) DO NOTHING
            """
            |> Sql.parameters [
                "id",  Sql.uuid a.Id
                "rid", Sql.uuid a.RuleId
                "rn",  Sql.string a.RuleName
                "sev", Sql.string (severityToStr a.Severity)
                "msg", Sql.string a.Message
                "v",   Sql.double a.Value
                "t",   Sql.double a.Threshold
                "ta",  Sql.timestamptz a.TriggeredAt
            ]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        ()
    } |> Async.Start   // fire-and-forget

let acknowledgeAlert (cs: string) (id: Guid) : Async<unit> =
    async {
        let! _ =
            cs
            |> Sql.connect
            |> Sql.query "UPDATE pgm_alerts SET acknowledged = true WHERE id = @id"
            |> Sql.parameters ["id", Sql.uuid id]
            |> Sql.executeNonQueryAsync
            |> Async.AwaitTask
        alerts <- alerts |> List.map (fun a -> if a.Id = id then { a with Acknowledged = true } else a)
    }

// ── Evaluation engine ─────────────────────────────────────────────────────────

let private evaluate (metric: string) (value: float) (rule: AlertRule) : Alert option =
    if not rule.Enabled || rule.Metric <> metric then None
    else
        let triggered =
            match metric with
            | "cache_hit_ratio" -> value < rule.Threshold
            | _                 -> value > rule.Threshold
        if triggered then
            Some {
                Id           = Guid.NewGuid()
                RuleId       = rule.Id
                RuleName     = rule.Name
                Severity     = rule.Severity
                Message      = $"{rule.Name}: {metric} = {value:F2} (threshold {rule.Threshold:F2})"
                Value        = value
                Threshold    = rule.Threshold
                TriggeredAt  = DateTime.UtcNow
                ResolvedAt   = None
                Acknowledged = false
            }
        else None

let checkMetric (cs: string) (metric: string) (value: float) : Alert list =
    let fired =
        rules
        |> List.choose (evaluate metric value)
        |> List.filter (fun a ->
            alerts
            |> List.exists (fun existing -> existing.RuleId = a.RuleId && existing.ResolvedAt.IsNone)
            |> not)
    if not fired.IsEmpty then
        alerts <- fired @ alerts
        fired |> List.iter (persistAlert cs)
    fired

let resolveMetric (metric: string) (value: float) =
    alerts <- alerts |> List.map (fun a ->
        let belongsToMetric =
            a.ResolvedAt.IsNone &&
            rules |> List.exists (fun r -> r.Id = a.RuleId && r.Metric = metric)
        if belongsToMetric then
            let shouldFire =
                rules
                |> List.tryFind (fun r -> r.Id = a.RuleId)
                |> Option.map (fun r ->
                    match metric with
                    | "cache_hit_ratio" -> value < r.Threshold
                    | _                 -> value > r.Threshold)
                |> Option.defaultValue false
            if not shouldFire then { a with ResolvedAt = Some DateTime.UtcNow }
            else a
        else a)
