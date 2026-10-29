# Shared Kernel

Single solution, folder-layered DDD (not multi-project) to keep `IoBuild.sln` simple
and `docker compose` contracts stable.

- `Shared/Domain` — `IBaseRepository`, `IUnitOfWork`, `DomainEvent`, `IAggregateRoot`
- `Shared/Infrastructure/Persistence/EFC` — `IoBuildDbContext` remains the **single**
  `DbContext` with 5 migrations (`Persistence/2026*.cs`). Per-BC
  `Infrastructure/Persistence/EFC/Configuration/*` files configure their tables
  but are still applied from `IoBuildDbContext.OnModelCreating` (no split DB).
- `Shared/Interfaces/ASP/Configuration` — kebabCase/snake_case conventions,
  CORS, forwarded headers. Wire contracts (`/api/v1/*`) unchanged.
- `Contracts`, `Workflows`, `Readiness`, `Observability` are Shared as well.

This keeps `frontend`, `nginx`, `docker-compose.yml`, `InfluxDB`,
`Mosquitto`, and `simulator` wire contracts identical while making the
bounded-context layering explicit for IoT / Gerencia / DevSecOps reuse.
