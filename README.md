# PgMonitor

A production-grade PostgreSQL observability platform built with **F# 10** (ASP.NET Core Minimal APIs) and **Svelte 4 + Tailwind CSS**, served behind a **Traefik** load balancer.

---

## Features

| Feature | Details |
|---|---|
| **Real-time metrics** | CPU, disk I/O, cache hit ratio pushed via SignalR WebSocket every 3 seconds |
| **Live query tracking** | `pg_stat_activity` — duration, state, wait events, client address |
| **Slow query analysis** | `pg_stat_statements` — mean/max/total time, cache hit %, stddev |
| **SQL Runner** | Execute any SQL statement; results displayed as a plain table of strings |
| **EXPLAIN Analyzer** | `EXPLAIN (ANALYZE, BUFFERS)` with syntax-highlighted plan output |
| **Replication monitoring** | Streaming replicas (write/flush/replay lag), replication slots with lag bytes |
| **Lock detection** | All locks with granted/waiting status and blocking chain |
| **Vacuum tracking** | Active vacuum progress + table bloat analysis |
| **Alerting** | Persistent rule engine (CPU, cache hit, connections, query duration, replication lag), acknowledge flow |
| **User management** | Create/delete users, change passwords — admin role required |
| **Multiple DB connections** | Add/remove/test additional PostgreSQL connections; SQL Runner targets any connection |
| **JWT Auth** | HS256 tokens, 8h expiry, role-based (`admin` / `viewer`), SignalR token-in-querystring support |
| **Structured logging** | Serilog → console + Seq, with correlation IDs on every request |
| **Data Protection** | ASP.NET Core Data Protection keys persisted to filesystem, encrypted with a self-signed X.509 certificate |
| **Load balancing** | Traefik v3 — auto-discovers containers via Docker labels, sticky sessions for SignalR |
| **Health checks** | `/healthz` endpoint (includes Postgres connectivity); Traefik respects it before routing traffic |

---

## Stack

| Layer | Technology |
|---|---|
| Backend | F# 10, ASP.NET Core Minimal APIs, SignalR, Npgsql.FSharp 5.7 |
| Frontend | Svelte 4, Vite, Tailwind CSS 3, svelte-spa-router |
| Auth | JWT (HS256), BCrypt |
| Logging | Serilog, Serilog.Sinks.Seq |
| Load balancer | Traefik v3 |
| Database | PostgreSQL 16+ with `pg_stat_statements` |
| Observability | Seq (structured log UI) |

---

## Quick Start

### Prerequisites

- Docker Desktop with the WSL2 backend
- Port 80 free (Traefik ingress)

### Run

```bash
cp backend/.env.example backend/.env
# Edit backend/.env with your secrets

docker compose up -d
```

| URL | Service |
|---|---|
| http://localhost | PgMonitor UI |
| http://localhost:8080 | Seq log UI |
| http://localhost:8090 | Traefik dashboard |

### Scale

Traefik auto-discovers new containers within seconds — no restart required:

```bash
docker compose up --scale backend=3 --scale frontend=2 -d
```

---

## Environment Variables

### Backend (`.env` / `.env.production`)

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__Postgres` | Primary PostgreSQL connection string | `Host=localhost;...` |
| `Jwt__Key` | JWT signing secret (min 32 chars) | — |
| `Seq__Url` | Seq ingestion URL | `http://localhost:5341` |
| `Cors__Origins` | Comma-separated allowed origins | `http://localhost` |
| `ASPNETCORE_URLS` | Kestrel listen URL | `http://+:5000` |

### Compose-level

| Variable | Description |
|---|---|
| `POSTGRES_USER` | PostgreSQL superuser |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `POSTGRES_DB` | Default database |
| `SEQ_FIRSTRUN_ADMINPASSWORD` | Seq admin password (first run only) |

---

## Authentication

Default credentials created on first startup:

| User | Password | Role |
|---|---|---|
| `admin` | `admin123` | admin |
| `viewer` | `viewer123` | viewer |

Change these immediately via **Settings → Users** or the API. Credentials are stored in `pgm_users` with BCrypt hashes.

Roles:
- **admin** — full access including user management, connection management, alert rule CRUD
- **viewer** — read-only access to all monitoring data

---

## Architecture

```
pgmonitor/
├── backend/
│   ├── src/
│   │   ├── Models.fs           # All domain types
│   │   ├── Schema.fs           # DB init — creates pgm_* tables, seeds defaults
│   │   ├── Database.fs         # Npgsql.FSharp queries + SQL runner
│   │   ├── Metrics.fs          # OS metrics (/proc/stat, /proc/diskstats)
│   │   ├── ConnectionStore.fs  # Multi-connection registry (in-memory + pgm_connections)
│   │   ├── Auth.fs             # JWT generation, BCrypt, user CRUD (pgm_users)
│   │   ├── Alerts.fs           # Alert rule engine, persisted to pgm_alert_rules/pgm_alerts
│   │   ├── Hubs.fs             # SignalR hub (authorized)
│   │   ├── Handlers.fs         # Minimal API handlers with structured logging
│   │   └── Program.fs          # Startup, middleware, routing, broadcast loop
│   ├── appsettings.json
│   ├── Dockerfile
│   └── PgMonitor.fsproj
│
├── frontend/
│   ├── src/
│   │   ├── stores/
│   │   │   ├── auth.js         # JWT store + apiFetch helper
│   │   │   ├── hub.js          # SignalR connection + reactive stores
│   │   │   └── connections.js  # DB connection store
│   │   └── components/
│   │       ├── Login.svelte
│   │       ├── Sidebar.svelte
│   │       ├── Overview.svelte
│   │       ├── LiveQueries.svelte
│   │       ├── SlowQueries.svelte
│   │       ├── SqlRunner.svelte        # Plain SQL execution, tabular results
│   │       ├── SqlEditor.svelte        # EXPLAIN ANALYZE
│   │       ├── Replication.svelte
│   │       ├── Locks.svelte
│   │       ├── Vacuum.svelte
│   │       ├── Alerts.svelte
│   │       ├── UserManagement.svelte   # Admin only
│   │       └── ConnectionManager.svelte # Admin only
│   ├── nginx.conf              # Static file server (Traefik handles /api and /hubs)
│   └── Dockerfile
│
├── docker-compose.yml
└── README.md
```

---

## Database Schema

PgMonitor creates its own tables in the monitored database on first startup:

| Table | Contents |
|---|---|
| `pgm_users` | Application users (username, BCrypt hash, role) |
| `pgm_connections` | Additional monitored database connections |
| `pgm_alert_rules` | Alert rule definitions |
| `pgm_alerts` | Fired alert history (last 500 retained in memory) |

---

## API Reference

All endpoints except `/api/auth/login` require `Authorization: Bearer <token>`.
Endpoints marked **admin** additionally require the `admin` role.

### Auth
| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/login` | Returns JWT `{ token, expiresAt }` |

### Users (admin)
| Method | Path | Description |
|---|---|---|
| GET | `/api/users` | List all users |
| POST | `/api/users` | Create user `{ username, password, role }` |
| DELETE | `/api/users/{id}` | Delete user |
| PATCH | `/api/users/{id}/password` | Change password `{ newPassword }` |

### Connections
| Method | Path | Description |
|---|---|---|
| GET | `/api/connections` | List connections (passwords redacted) |
| POST | `/api/connections` | Add connection (admin) |
| DELETE | `/api/connections/{id}` | Remove connection (admin) |
| POST | `/api/connections/test` | Test connectivity (admin) |

### Monitoring
| Method | Path | Description |
|---|---|---|
| GET | `/api/snapshot` | CPU + disk + IO + connections |
| GET | `/api/queries/active` | Live queries |
| GET | `/api/queries/slow?limit=50` | Top slow queries |
| POST | `/api/queries/explain` | EXPLAIN ANALYZE `{ query, database }` |
| POST | `/api/sql/run` | Run SQL `{ connectionId?, sql }` |
| GET | `/api/replication` | Replicas + slots |
| GET | `/api/locks` | All locks |
| GET | `/api/vacuum/activity` | Active vacuum progress |
| GET | `/api/vacuum/bloat` | Table bloat |
| GET | `/healthz` | Health check (public) |

### Alerts
| Method | Path | Description |
|---|---|---|
| GET | `/api/alerts` | Fired alerts |
| GET | `/api/alerts/rules` | Alert rules |
| POST | `/api/alerts/rules` | Create rule (admin) |
| DELETE | `/api/alerts/rules/{id}` | Delete rule (admin) |
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge alert |

### SignalR
| Endpoint | Description |
|---|---|
| `WS /hubs/monitor?access_token=<jwt>` | Real-time feed |

| Event | Payload |
|---|---|
| `snapshot` | `SystemSnapshot` — CPU, disks, IO, connections |
| `activeQueries` | `ActiveQuery[]` |
| `alerts` | `Alert[]` — newly fired alerts only |

---

## Correlation IDs

Every HTTP request is assigned a correlation ID (`X-Correlation-Id` header). If the caller provides this header it is reused, otherwise a new UUID is generated. The ID is:

- Echoed back in the response as `X-Correlation-Id`
- Attached to every Serilog log event as the `CorrelationId` property
- Visible in Seq — use `@Properties['CorrelationId'] = '...'` to filter all events for a single request

---

## Data Protection

ASP.NET Core Data Protection keys are persisted to `/var/lib/pgmonitor/keys` (Docker volume `dpkeys`). Keys are encrypted at rest using a self-signed RSA-2048 X.509 certificate (`dp-cert.pfx`) stored in the same directory and generated automatically on first startup.

The certificate has a 10-year validity. **Do not delete the `dpkeys` volume** — doing so would invalidate all existing encrypted sessions.

---

## Load Balancer

Traefik v3 is used as the ingress and load balancer:

- Routes `/api/*` and `/hubs/*` → backend (priority 10)
- Routes `/*` → frontend (priority 1)
- Sticky sessions via cookie `pgm_instance` keep each browser's SignalR connection on the same backend instance
- Backend health check on `/healthz` — traffic only routed to healthy instances
- Dashboard at http://localhost:8090

---

## PostgreSQL Setup (manual / existing instance)

```sql
-- Required for slow query tracking
ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements';
ALTER SYSTEM SET pg_stat_statements.track = 'all';
ALTER SYSTEM SET track_io_timing = 'on';
SELECT pg_reload_conf();

CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
```

---

## Production Checklist

- [ ] Change `Jwt__Key` to a strong random secret (min 32 chars)
- [ ] Change default `admin` / `viewer` passwords via the Users UI
- [ ] Set `SEQ_FIRSTRUN_ADMINPASSWORD` to a strong password
- [ ] Set `POSTGRES_PASSWORD` to a strong password
- [ ] Back up the `dpkeys` volume (contains encryption certificate)
- [ ] Put a TLS-terminating reverse proxy (Nginx, Caddy) in front of Traefik for HTTPS
- [ ] Set `Cors__Origins` to your actual domain
- [ ] Restrict port `5445` (Postgres) and `5341` / `8080` (Seq) to internal networks only
