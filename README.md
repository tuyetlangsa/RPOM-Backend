# Rpom Backend

Restaurant POS and Operations Management Platform — .NET 10 backend.

## Stack

- **.NET 10** + Minimal API + MediatR + FluentValidation
- **Postgres 17 + pgvector** *(via Npgsql.EntityFrameworkCore.PostgreSQL)*
- **Quartz.NET** *(background jobs + outbox)*
- **Serilog → Seq** *(log viewer)*
- **JWT auth + BCrypt** *(custom — permission-based, not role-inheritance)*
- **Anthropic Claude SDK** *(AI Operations Assistant)*

## Solution layout

```
Rpom-backend/
├── src/
│   ├── Rpom.Domain          # entities, value objects, domain events, Result<T>
│   ├── Rpom.Application     # MediatR handlers, validators, abstractions
│   ├── Rpom.Infrastructure  # EF Core, JWT, outbox, AI, Quartz
│   ├── Rpom.Api             # Minimal API endpoints + Swagger
│   └── Rpom.Worker          # Quartz background jobs
└── tests/
    ├── Rpom.Domain.Tests
    └── Rpom.Application.Tests
```

See `CLAUDE.md` for detailed architecture + endpoint pattern + permission flow.

## Quick start (Docker)

```bash
docker compose up --build
# API at http://localhost:5000
# Seq at http://localhost:8081
```

## Quick start (local)

```bash
# 1. Postgres + Seq only
docker compose up -d postgres seq

# 2. Migrations
dotnet ef database update --project src/Rpom.Infrastructure --startup-project src/Rpom.Api

# 3. Run API
dotnet run --project src/Rpom.Api
```

## Common commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Add migration
dotnet ef migrations add <Name> --project src/Rpom.Infrastructure --startup-project src/Rpom.Api

# Apply migrations
dotnet ef database update --project src/Rpom.Infrastructure --startup-project src/Rpom.Api
```

## Configuration

Secrets via `appsettings.Development.json` (gitignored) or env vars. Production overrides via `appsettings.Production.json`.
