# Week 5 test strategy – diagrams

Visual summary of [TESTING-STRATEGY.md](../../TESTING-STRATEGY.md). The numbers are the real counts from the
full run after step 08 (section 3.8 of the strategy): **1,941 passed, 0 failed, 0 skipped**.
The diagrams are Mermaid and render directly on GitHub.

## 1. How the Week 5 work builds up

Each box is a group of Week 5 steps. An arrow means "builds on". Unit tests come first; mocking, TDD and BDD
reuse their rules and helpers; everything meets in the integrated suite that CI runs on every push.

```mermaid
flowchart TB
    G0["Group 0: Strategy and restructure<br/>Steps 01–02"]
    G1["Group 1: Unit testing<br/>Step 03"]
    G2["Group 2: Mocking and isolation<br/>Steps 04–06"]
    G3["Group 3: TDD<br/>Step 07"]
    G4["Group 4: BDD with Reqnroll<br/>Step 08"]
    G5["Group 5: Copilot<br/>Step 09"]
    G6["Group 6: Integrated suite<br/>CI + final PR"]

    G0 --> G1
    G1 --> G2
    G1 --> G3
    G1 --> G4
    G2 --> G3
    G2 --> G6
    G3 --> G6
    G4 --> G5
    G4 --> G6
    G5 --> G6

    classDef group fill:#0b2a4a,stroke:#3b6fa0,color:#cfe3ff,font-weight:bold
    class G0,G1,G2,G3,G4,G5,G6 group
```

| Group | Steps | What it delivers | Main files |
| --- | --- | --- | --- |
| 0 Strategy and restructure | 01, 02 | Risk map, pyramid, review of existing tests, one SQL Server lookup, `Category` trait on every class, fixed `FakeTimeProvider` in both test hosts | `TESTING-STRATEGY.md`, `tests/Common/` |
| 1 Unit testing | 03 | Full 6×6 status-transition matrix, tracking-number and courier-id theories, weight / page / upload boundaries, exact exception type and message | `ShipmentStatusRulesTests`, `TrackingLimitsTests` |
| 2 Mocking and isolation | 04, 05, 06 | Moq for service ports, fake and Moq `Protected()` HTTP handlers, real SQL Server repositories, `WebApplicationFactory` end-to-end with a test auth scheme | `ShipmentTrackingServiceMoqTests`, `ApexApiClientHttpHandlerTests`, `TrackingRepositorySqlTests`, `ShipmentLifecycleEndToEndTests` |
| 3 TDD | 07 | 72-hour scan-age rule built RED → GREEN → TEST → REFACTOR | `ScanAgeRuleTests`, `ScanAgePolicy`, `TDD-LOG.md` |
| 4 BDD with Reqnroll | 08 | Shipment creation (API level) and scan sync (service level) features with Background and Scenario Outline | `tests/ApexPost.Specs/Features/` |
| 5 Copilot | 09 | Prompts, kept / changed / rejected decisions, mutation proofs | `docs/copilot/`, `COPILOT-LOG.md` |
| 6 Integrated suite | all | Build with 0 warnings, full suite in CI with a SQL Server container, 0 skipped | `.github/workflows/ci.yml` |

## 2. Test pyramid (real counts after step 08)

Rule: **prove each rule once, at the lowest level that can prove it. Higher levels check wiring only.**

```mermaid
flowchart TB
    S["BDD / Spec – 12 cases (0.6 %)<br/>target ~5 %<br/>ApexPost.Specs"]
    I["Integration – 768 cases (39.6 %)<br/>target ~25 %<br/>SQL Server, Couchbase, WebApplicationFactory"]
    U["Unit – 1,161 cases (59.8 %)<br/>target ~70 %<br/>rules, limits, mappers, Moq, fake HTTP"]

    S --> I --> U

    classDef spec fill:#5b2a86,stroke:#9d6fd0,color:#f1e6ff,font-weight:bold
    classDef integ fill:#0b4a5a,stroke:#3b9fb0,color:#d6f6ff,font-weight:bold
    classDef unit fill:#1b5e3a,stroke:#4caf7a,color:#dcffe9,font-weight:bold
    class S spec
    class I integ
    class U unit
```

The integration share is above target on purpose: the Week 3 server-rendered pages have no other tests
(strategy section 3.9). The Spec share is small on purpose: each scenario shows a business outcome whose rules
are already proved below (strategy section 9).

## 3. Test projects, levels and the code they cover

```mermaid
flowchart LR
    subgraph UNIT["Unit – Category=Unit"]
        UT["ApexPost.UnitTests<br/>534 cases"]
        TU["ApexPost.Tracking.UnitTests<br/>243 cases"]
        CU["Courier.UnitTests<br/>75 cases"]
        AC["Courier.ApiClient.Tests<br/>180 cases"]
        PT["Courier.Presentation.Tests<br/>53 cases (kept, not extended)"]
    end

    subgraph INTEG["Integration – Category=Integration"]
        IT["ApexPost.IntegrationTests<br/>450 cases"]
        AT["ApexPost.Api.Tests<br/>330 cases"]
        OT["Courier.Offline.Tests<br/>64 cases"]
    end

    subgraph BDD["BDD – Category=Spec"]
        SP["ApexPost.Specs<br/>12 cases"]
    end

    DOM["Domain + Application<br/>(incl. Courier.Domain)<br/>services and rules"]
    API["ApexPost.Api<br/>controllers, validation, security"]
    INF["Infrastructure<br/>EF Core + SQL Server"]
    CLI["Typed API client<br/>JSON, ProblemDetails, Polly"]
    OFF["Couchbase Lite<br/>outbox + repository"]

    UT --> DOM
    TU --> DOM
    CU --> DOM
    CU --> OFF
    AC --> CLI
    IT --> INF
    IT --> DOM
    AT --> API
    OT --> OFF
    SP --> API
    SP --> OFF

    classDef unit fill:#1b5e3a,stroke:#4caf7a,color:#dcffe9
    classDef integ fill:#0b4a5a,stroke:#3b9fb0,color:#d6f6ff
    classDef spec fill:#5b2a86,stroke:#9d6fd0,color:#f1e6ff
    classDef code fill:#0b2a4a,stroke:#3b6fa0,color:#cfe3ff
    class UT,TU,CU,AC,PT unit
    class IT,AT,OT integ
    class SP spec
    class DOM,API,INF,CLI,OFF code
```

## 4. Test doubles: which one where

```mermaid
flowchart TB
    Q{"What does the code<br/>under test depend on?"}
    Q -->|"Service port<br/>(repository, unit of work,<br/>current courier, uploader)"| M["Moq<br/>Setup / ReturnsAsync / ThrowsAsync / Verify"]
    Q -->|"HTTP"| H["Fake HttpMessageHandler<br/>+ Moq Protected() SendAsync<br/>no real network"]
    Q -->|"EF Core DbContext"| D["Real throw-away SQL Server database<br/>never mock DbContext / DbSet"]
    Q -->|"Couchbase Lite"| C["Real embedded Couchbase<br/>in a unique temp folder"]
    Q -->|"Time"| T["FakeTimeProvider<br/>no DateTime.UtcNow"]
    Q -->|"Whole API host"| W["WebApplicationFactory<br/>+ TestAuthHandler scheme"]
    Q -->|"Stateful store in BDD"| F["Hand-written in-memory fake"]

    classDef q fill:#0b2a4a,stroke:#3b6fa0,color:#cfe3ff,font-weight:bold
    classDef d fill:#1d2733,stroke:#66758a,color:#f4f6f9
    class Q q
    class M,H,D,C,T,W,F d
```

The SQL Server lookup order is the same for every SQL test: CI variable `ConnectionStrings__ApexPostTestServer`
→ user-secrets → LocalDB on Windows → `Assert.Skip` locally (never skipped in CI).

## 5. Risks and the level that proves them

```mermaid
flowchart LR
    R1["R1 Status moves backwards"] --> UL["Unit"]
    R2["R2 Duplicate tracking number"] --> UL
    R2 --> IL["Integration"]
    R2 --> BL["BDD"]
    R3["R3 Re-sent scan stored twice"] --> UL
    R3 --> IL
    R3 --> BL
    R4["R4 Courier impersonation / roles"] --> UL
    R4 --> IL
    R5["R5 Client retries a 4xx"] --> UL
    R6["R6 Outbox loses a scan"] --> UL
    R6 --> IL
    R6 --> BL
    R7["R7 Off-by-one limits"] --> UL
    R8["R8 Stack trace leaks"] --> IL
    R9["R9 v1 contract drifts"] --> IL
    R10["R10 Week 3 money / stock"] --> UL
    R10 --> IL
    R11["R11 Wall-clock flakiness"] --> UL
    R11 --> IL
    R11 --> BL
    R12["R12 Scan older than 72 h"] --> UL
    R12 --> IL
    R12 --> BL

    classDef risk fill:#5a1020,stroke:#c8102e,color:#ffe3e8
    classDef unit fill:#1b5e3a,stroke:#4caf7a,color:#dcffe9,font-weight:bold
    classDef integ fill:#0b4a5a,stroke:#3b9fb0,color:#d6f6ff,font-weight:bold
    classDef spec fill:#5b2a86,stroke:#9d6fd0,color:#f1e6ff,font-weight:bold
    class R1,R2,R3,R4,R5,R6,R7,R8,R9,R10,R11,R12 risk
    class UL unit
    class IL integ
    class BL spec
```

Full risk table with likelihood, cost and test classes: [TESTING-STRATEGY.md section 2](../../TESTING-STRATEGY.md#2-risk-map).
