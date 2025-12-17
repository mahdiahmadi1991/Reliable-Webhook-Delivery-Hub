# Reliable Webhook Delivery Hub

This repository hosts the scaffolding for a minimal API-based solution targeting .NET 10.

## Getting started
1. Copy the example environment file and provide a strong SQL Server password:
   ```bash
   cp .env.example .env
   ```
2. Start the infrastructure dependencies:
   ```bash
   docker compose up -d
   ```
   SQL Server will listen on `localhost:14333` and Redis on `localhost:6380`.
3. Run the API (loads `.env` automatically in Development):
   ```bash
   dotnet run --project src/Api
   ```
   The listening port is shown in the console output.
4. Explore the API:
   - Swagger UI: `http://localhost:<api-port>/swagger`
   - Health check: `http://localhost:<api-port>/health`
   - Root ping: `http://localhost:<api-port>/` returns `{ "status": "running" }`

## Projects
- `src/Api`: Minimal API host.
- `src/Application`: Application layer placeholders.
- `src/Domain`: Domain model placeholders.
- `src/Infrastructure`: Infrastructure placeholders.
- `tests/UnitTests`: xUnit unit tests.
- `tests/IntegrationTests`: xUnit integration tests.

## Tooling
- Target framework: `net10.0`.
- SDK version pinned in `global.json` to `10.0.100` with roll forward to the latest patch.
