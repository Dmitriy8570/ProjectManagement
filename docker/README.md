# Docker

The setup supports any combination of:

| Frontend          | Database |
| ----------------- | -------- |
| Vue 3 SPA (nginx) | SQLite   |
| Vue 3 SPA (nginx) | MSSQL    |
| Razor MVC         | SQLite   |
| Razor MVC         | MSSQL    |

Selection is controlled by two compose profiles (`vue` / `razor`, plus
optional `mssql`) and the `DB_PROVIDER` / `DB_CONN_STR` env vars.

## One-time setup

```bash
cp .env.example .env
# edit .env to pick SQLite or MSSQL (uncomment the appropriate block)
```

All commands below are run from the repository root.

## Vue SPA + SQLite (default)

```bash
docker compose --profile vue up --build
```

- Vue app: <http://localhost:5173>
- API (direct, e.g. for Swagger in Development): <http://localhost:5266/swagger>
- SQLite file lives in the `api-data` named volume.

## Vue SPA + MSSQL

In `.env`, switch to the MSSQL block (see comments), then:

```bash
docker compose --profile vue --profile mssql up --build
```

## Razor MVC + SQLite (default)

```bash
docker compose --profile razor up --build
```

- Razor app: <http://localhost:5100>
- SQLite file lives in the `web-data` named volume.

## Razor MVC + MSSQL

In `.env`, switch to the MSSQL block, then:

```bash
docker compose --profile razor --profile mssql up --build
```

## Stopping / cleaning up

```bash
docker compose --profile vue --profile razor --profile mssql down
# add -v to wipe SQLite/MSSQL data volumes
docker compose --profile vue --profile razor --profile mssql down -v
```

## Notes

- The Vue container's nginx reverse-proxies `/api/` to the `api` service,
  so the browser sees a single origin (mirrors the Vite dev proxy).
- The Razor app (`web` service) talks to the database directly and does
  not require the `api` service.
- `Database__Provider` accepts `Sqlite` or `SqlServer`. The connection
  string is read from `ConnectionStrings__DefaultConnection`. Both are
  set from `.env` via compose.
- The MSSQL container uses the `Developer` edition; change
  `MSSQL_SA_PASSWORD` for anything that isn't a local sandbox.
- Migrations run automatically on app startup (`Database.Migrate()` in
  `Program.cs`). The existing migrations were generated against SQLite;
  if you hit provider-specific issues against MSSQL, regenerate them
  with `dotnet ef migrations add ... --context AppDbContext` while
  pointing at MSSQL.
