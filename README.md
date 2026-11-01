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

## Run it

The demo data is created only in Development, and the shared demo password comes from user-secrets:

```bash
dotnet user-secrets set "Seed:DemoPassword" "<a password of your choice>" --project src/ApexPost.Web
dotnet dev-certs https --trust
dotnet run --project src/ApexPost.Web --launch-profile https
```

Open <https://localhost:7242> and sign in as `clerk1.br001@apexpost.test`, `manager.br001@apexpost.test`,
`auditor1@apexpost.test` or `admin1@apexpost.test`. The sign-in cookie is `Secure`, so use the HTTPS address.

## How the code is organised

- `src/ApexPost.Domain` — domain model; references no other project.
- `src/ApexPost.Application` — use cases and ports; references Domain.
- `src/ApexPost.Infrastructure` — implementations of the ports; references Application.
- `src/ApexPost.Web` — ASP.NET Core MVC front end and composition root; references Application and Infrastructure.
- `tests/ApexPost.UnitTests` — xUnit unit tests and layering tests (`Architecture/`).
- `tests/ApexPost.IntegrationTests` — xUnit tests against the Web project (`Architecture/` holds the controller
  layering test).
- `.github/workflows/ci.yml` — restore, Release build and tests (measurement tests excluded) on every push and PR.
