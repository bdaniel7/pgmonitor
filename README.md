# PgMonitor

A production-grade PostgreSQL observability platform built with **F# 10** (ASP.NET Core) and **Svelte + Tailwind CSS**.

## Features

| Feature | Details |
|---|---|
| **Real-time metrics** | CPU, disk I/O, cache hit ratio via SignalR WebSocket push (3s interval) |
| **Live query tracking** | pg_stat_activity with duration, wait events, state |
| **Slow query analysis** | pg_stat_statements — mean/max/total time, cache hit %, full query |
| **SQL Editor** | EXPLAIN (ANALYZE, BUFFERS) with syntax-highlighted plan output |
| **Replication status** | Streaming replicas (lag: write/flush/replay), replication slots with lag bytes |
| **Lock detection** | All locks with granted/waiting status, blocking chain visualization |
| **Vacuum tracking** | Active vacuum progress + table bloat analysis |
| **Alerting** | Rule engine (CPU, cache hit, connections, query duration, replication lag), acknowledge flow |
| **JWT Auth** | HS256 tokens, 8h expiry, SignalR token-in-querystring support |

## Quick Start

### Docker (recommended)

```bash
docker compose up -d
```

- Frontend: http://localhost:4173
- Backend API: http://localhost:5000

### Manual development

**Prerequisites:** .NET 10 SDK, Node.js 22, PostgreSQL 16+

#### PostgreSQL setup

```sql
-- Enable pg_stat_statements
ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements';
ALTER SYSTEM SET pg_stat_statements.track = 'all';
ALTER SYSTEM SET track_io_timing = 'on';
SELECT pg_reload_conf();

-- Create the extension
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
```

#### Backend

```bash
cd backend
# Edit appsettings.json — set your connection string and JWT key
dotnet run
# API available at http://localhost:5000
```

#### Frontend

```bash
cd frontend
npm install
npm run dev
# UI available at http://localhost:5173
```

## Authentication

Default credentials (change in `Auth.fs` for production):

| User | Password | Role |
|---|---|---|
| admin | admin123 | admin |
| viewer | viewer123 | viewer |

Tokens are HS256 JWT, valid for 8 hours.

## Architecture

```
pgmonitor/
├── backend/
│   ├── src/
│   │   ├── Models.fs       # All domain types (DU, records)
│   │   ├── Database.fs     # Npgsql.FSharp queries
│   │   ├── Metrics.fs      # OS metrics (/proc/stat, /proc/diskstats)
│   │   ├── Auth.fs         # JWT generation & bcrypt
│   │   ├── Alerts.fs       # In-memory alert rule engine
│   │   ├── Hubs.fs         # SignalR hub (authorized)
│   │   ├── Handlers.fs     # Minimal API handlers
│   │   └── Program.fs      # Startup, routing, broadcast loop
│   └── PgMonitor.fsproj
│
└── frontend/
    └── src/
        ├── stores/
        │   ├── auth.js     # JWT store + apiFetch helper
        │   └── hub.js      # SignalR connection + reactive stores
        └── components/
            ├── Login.svelte
            ├── Sidebar.svelte
            ├── Overview.svelte     # CPU/disk/IO/connections dashboard
            ├── LiveQueries.svelte  # Real-time pg_stat_activity
            ├── SlowQueries.svelte  # pg_stat_statements analysis
            ├── SqlEditor.svelte    # EXPLAIN ANALYZE with plan highlighting
            ├── Replication.svelte  # Replicas + slots
            ├── Locks.svelte        # Lock detection
            ├── Vacuum.svelte       # Active vacuum + bloat table
            └── Alerts.svelte       # Alert list + rule management
```

## API Reference

All endpoints (except `/api/auth/login`) require `Authorization: Bearer <token>`.

| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/login` | Get JWT token |
| GET | `/api/snapshot` | CPU + disk + IO + connections |
| GET | `/api/queries/active` | Live queries from pg_stat_activity |
| GET | `/api/queries/slow?limit=50` | pg_stat_statements top queries |
| POST | `/api/queries/explain` | EXPLAIN ANALYZE `{ query, database }` |
| GET | `/api/replication` | Replicas + slots |
| GET | `/api/locks` | All locks |
| GET | `/api/vacuum/activity` | Active vacuum progress |
| GET | `/api/vacuum/bloat` | Table bloat analysis |
| GET | `/api/alerts` | Fired alerts |
| GET | `/api/alerts/rules` | Alert rules |
| POST | `/api/alerts/rules` | Create rule |
| DELETE | `/api/alerts/rules/{id}` | Delete rule |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge alert |
| WS | `/hubs/monitor?access_token=<jwt>` | SignalR real-time feed |

## SignalR Events (client receives)

| Event | Payload |
|---|---|
| `snapshot` | `SystemSnapshot` — CPU, disks, IO, connections |
| `activeQueries` | `ActiveQuery[]` — live queries |
| `alerts` | `Alert[]` — newly fired alerts |

## Production Considerations

1. **JWT secret** — set a strong key in `appsettings.json` or via env var `Jwt__Key`
2. **Connection string** — `ConnectionStrings__Postgres` env var
3. **TLS** — put Nginx/Caddy in front for HTTPS
