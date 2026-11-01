# Apex_Post_Logistic

ApexPost Counter Platform: an ASP.NET Core MVC application (.NET 10, C# 14) for postal counters that sell
retail goods together with shipping, money orders and identity verification in one basket.

## Requirements

- .NET 10 SDK (see `global.json`)

## Build and test

```bash
dotnet build Apex_Post_Logistic.slnx -c Release
dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"
```

## How the code is organised

- `src/ApexPost.Domain` — domain model; references no other project.
- `src/ApexPost.Application` — use cases and ports; references Domain.
- `src/ApexPost.Infrastructure` — implementations of the ports; references Application.
- `src/ApexPost.Web` — ASP.NET Core MVC front end and composition root; references Application and Infrastructure.
- `tests/ApexPost.UnitTests` — xUnit unit tests and layering tests (`Architecture/`).
- `tests/ApexPost.IntegrationTests` — xUnit tests against the Web project (`Architecture/` holds the controller
  layering test).
- `.github/workflows/ci.yml` — restore, Release build and tests (measurement tests excluded) on every push and PR.
