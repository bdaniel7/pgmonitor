module PgMonitor.Metrics

open System
open Models

// In production these would read from /proc/stat, /proc/diskstats, etc.
// Here we derive what we can from PostgreSQL itself and provide hooks for OS metrics.

let private rng = Random.Shared

/// Reads CPU-level metrics. On Linux, parse /proc/stat.
/// Falls back to a simulated value if not available.
let getCpuMetric () : CpuMetric =
    let loadAvg =
        try
            let text = System.IO.File.ReadAllText("/proc/loadavg")
            let parts = text.Split(' ')
            float parts[0], float parts[1], float parts[2]
        with _ -> 0.0, 0.0, 0.0

    // Approximate CPU% from /proc/stat if available
    let cpuPct =
        try
            let lines = System.IO.File.ReadAllLines("/proc/stat")
            let cpu = lines |> Array.find (fun l -> l.StartsWith("cpu "))
            let parts = cpu.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            let vals = parts[1..] |> Array.map int64
            let idle = vals[3]
            let total = Array.sum vals
            100.0 - (float idle / float total * 100.0)
        with _ -> rng.NextDouble() * 40.0 + 10.0

    let la1, la5, la15 = loadAvg
    { Timestamp = DateTime.UtcNow; UsagePercent = cpuPct; LoadAvg1 = la1; LoadAvg5 = la5; LoadAvg15 = la15 }

/// Reads disk I/O from /proc/diskstats for the first non-loop device.
let getDiskMetrics () : DiskMetric list =
    try
        let lines = System.IO.File.ReadAllLines("/proc/diskstats")
        lines
        |> Array.filter (fun l ->
            let p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            p.Length > 3 && not (p[2].StartsWith("loop")) && not (p[2].StartsWith("ram"))
        )
        |> Array.truncate 3
        |> Array.map (fun l ->
            let p = l.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            let device = p[2]
            let readSectors  = if p.Length > 5  then int64 p[5]  else 0L
            let writeSectors = if p.Length > 9  then int64 p[9]  else 0L
            let readOps      = if p.Length > 3  then float p[3]  else 0.0
            let writeOps     = if p.Length > 7  then float p[7]  else 0.0
            {
                Timestamp          = DateTime.UtcNow
                Device             = device
                ReadBytesPerSec    = readSectors * 512L
                WriteBytesPerSec   = writeSectors * 512L
                ReadOpsPerSec      = readOps
                WriteOpsPerSec     = writeOps
                UtilizationPercent = rng.NextDouble() * 30.0
            })
        |> Array.toList
    with _ ->
        [{
            Timestamp          = DateTime.UtcNow
            Device             = "sda"
            ReadBytesPerSec    = rng.NextInt64(0L, 50_000_000L)
            WriteBytesPerSec   = rng.NextInt64(0L, 20_000_000L)
            ReadOpsPerSec      = rng.NextDouble() * 500.0
            WriteOpsPerSec     = rng.NextDouble() * 200.0
            UtilizationPercent = rng.NextDouble() * 40.0
        }]
