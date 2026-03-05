module PgMonitor.Models

open System
open System.Text.Json
open System.Text.Json.Serialization

// ── Auth ─────────────────────────────────────────────────────────────────────

type LoginRequest  = { Username: string; Password: string }
type LoginResponse = { Token: string; ExpiresAt: DateTime }

// ── Users ─────────────────────────────────────────────────────────────────────

type AppUser = {
    Id: Guid
    Username: string
    Role: string
    CreatedAt: DateTime
}

type CreateUserRequest    = { Username: string; Password: string; Role: string }
type ChangePasswordRequest = { NewPassword: string }

// ── Database Connections ─────────────────────────────────────────────────────

type DbConnection = {
    Id: Guid
    Name: string
    Host: string
    Port: int
    Database: string
    Username: string
    Password: string
    IsPrimary: bool
}

type CreateConnectionRequest = {
    Name: string
    Host: string
    Port: int
    Database: string
    Username: string
    Password: string
}

// ── System Metrics ────────────────────────────────────────────────────────────

type CpuMetric = {
    Timestamp: DateTime
    UsagePercent: float
    LoadAvg1: float
    LoadAvg5: float
    LoadAvg15: float
}

type DiskMetric = {
    Timestamp: DateTime
    Device: string
    ReadBytesPerSec: int64
    WriteBytesPerSec: int64
    ReadOpsPerSec: float
    WriteOpsPerSec: float
    UtilizationPercent: float
}

type IoMetric = {
    Timestamp: DateTime
    BlocksRead: int64
    BlocksHit: int64
    HitRatio: float
    TempFilesCreated: int64
    TempBytesWritten: int64
}

type SystemSnapshot = {
    Cpu: CpuMetric
    Disks: DiskMetric list
    Io: IoMetric
    Connections: int
    MaxConnections: int
    DatabaseSize: int64
}

// ── Query Tracking ────────────────────────────────────────────────────────────

type ActiveQuery = {
    Pid: int
    Username: string
    Database: string
    State: string
    Query: string
    Duration: TimeSpan
    WaitEventType: string option
    WaitEvent: string option
    ClientAddr: string option
    ApplicationName: string
}

type SlowQuery = {
    QueryId: int64
    Query: string
    Calls: int64
    TotalTime: float
    MeanTime: float
    StddevTime: float
    MinTime: float
    MaxTime: float
    Rows: int64
    SharedBlksHit: int64
    SharedBlksRead: int64
    HitPercent: float
}

type ExplainRequest  = { Query: string; Database: string }
type ExplainResponse = { Plan: string; ExecutionTime: float option }

// ── SQL Runner ────────────────────────────────────────────────────────────────

type SqlRunRequest = { ConnectionId: Guid option; Sql: string }

type SqlRunResult = {
    Columns: string list
    Rows: string list list
    RowCount: int
    ExecutionTimeMs: float
    Error: string option
}

// ── Replication ───────────────────────────────────────────────────────────────

type ReplicationSlot = {
    SlotName: string
    Plugin: string
    SlotType: string
    Active: bool
    RestartLsn: string option
    ConfirmedFlushLsn: string option
    LagBytes: int64 option
}

type ReplicaStatus = {
    ClientAddr: string
    State: string
    SentLsn: string
    WriteLsn: string
    FlushLsn: string
    ReplayLsn: string
    WriteLag: TimeSpan option
    FlushLag: TimeSpan option
    ReplayLag: TimeSpan option
    SyncState: string
}

type ReplicationStatus = {
    IsReplica: bool
    PrimaryInfo: string option
    Replicas: ReplicaStatus list
    Slots: ReplicationSlot list
}

// ── Locks ─────────────────────────────────────────────────────────────────────

type LockInfo = {
    Pid: int
    Username: string
    Query: string
    LockType: string
    Relation: string option
    Mode: string
    Granted: bool
    WaitingFor: int option
    Duration: TimeSpan
}

type BlockingChain = {
    BlockerPid: int
    BlockerQuery: string
    BlockedPids: int list
}

// ── Vacuum ────────────────────────────────────────────────────────────────────

type VacuumActivity = {
    Pid: int
    Database: string
    Schema: string
    Table: string
    Phase: string
    HeapBlksTotal: int64
    HeapBlksScanned: int64
    HeapBlksvacuumed: int64
    IndexVacuumCount: int64
    MaxDeadTuples: int64
    NumDeadTuples: int64
}

type TableBloat = {
    Schema: string
    TableName: string
    LiveTuples: int64
    DeadTuples: int64
    LastVacuum: DateTime option
    LastAutoVacuum: DateTime option
    LastAnalyze: DateTime option
    LastAutoAnalyze: DateTime option
    VacuumCount: int64
    BloatEstimatePercent: float
    SizeBytes: int64
}

// ── Alerts ────────────────────────────────────────────────────────────────────

[<JsonConverter(typeof<AlertSeverityConverter>)>]
type AlertSeverity = Info | Warning | Critical
and AlertSeverityConverter() =
    inherit JsonConverter<AlertSeverity>()
    with
        override _.Write(writer, value, options) =
            match value with
            | Info -> JsonSerializer.Serialize(writer, nameof(Info), options)
            | Warning -> JsonSerializer.Serialize(writer, nameof(Warning), options)
            | Critical -> JsonSerializer.Serialize(writer, nameof(Critical), options)
        override _.Read(reader, _, _) =
            match reader.GetString() with
            | "Info" -> Info
            | "Warning" -> Warning
            | "Critical" -> Critical
            | _ -> raise (JsonException "Expected Info or Warning or Critical")

type AlertRule = {
    Id: Guid
    Name: string
    Metric: string
    Threshold: float
    Severity: AlertSeverity
    Enabled: bool
}

type Alert = {
    Id: Guid
    RuleId: Guid
    RuleName: string
    Severity: AlertSeverity
    Message: string
    Value: float
    Threshold: float
    TriggeredAt: DateTime
    ResolvedAt: DateTime option
    Acknowledged: bool
}
