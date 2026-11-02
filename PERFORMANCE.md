# ApexPost Counter Platform: SQL performance

Every number here comes from a real run on SQL Server and links to the file that holds it. Anything that was not run is
listed under [Not verified](#not-verified). The decisions these measurements support are in
[DESIGN.md](DESIGN.md#6-architecture-decision-records): ADR-010, ADR-011, ADR-012 and ADR-017.

## Conclusions

- Three indexes were kept out of seven candidates. They cost 19.36 MB and about 4 % more log per sale.
- The largest gains came from indexes on `Payments.PaidAt` and `CounterTransactions (BranchCode, OpenedAt)`. The list of
  a branch fell from 11,633 logical reads to 88 and no longer sorts.
- Rewriting `usp_DailyBranchSales` with a temporary table saved more than any covering index, so the covering indexes
  were rejected.
- The worst estimate error came from parameter sniffing, not from statistics. It caused a tempdb spill, which
  `OPTION (RECOMPILE)` removed from that one rare report.
- Some workloads did not improve, and two got slower in elapsed time. They are shown below, not hidden.
- Low stock stays a scan. The table has 192 rows, and no index can beat 4 pages.

## Test environment

| Item | Value |
| --- | --- |
| Server | SQL Server 2025 RTM 17.0.1000.7, Enterprise Evaluation, in a VM |
| Hardware | 4 logical CPUs, 32 GB RAM, `max degree of parallelism` 4 |
| Database | `ApexPostDb_Perf`, a copy of the demo data plus the volume seed; compatibility level 170, SIMPLE recovery |
| Isolation | READ COMMITTED with `READ_COMMITTED_SNAPSHOT` off |
| Date | 2026-11-02 |

`ApexPostDb_Perf` is a separate database. The development database `ApexPostDb` was only migrated.

## Method

[sql/perf/Measure-Workloads.ps1](sql/perf/Measure-Workloads.ps1) runs each workload as follows:

1. It clears the plan cache of the database once, at the start of a phase.
2. It runs the workload once to warm the cache and compile the plan.
3. It runs it 5 more times with `SET STATISTICS IO, TIME ON`. Logical reads are from the first of these, and CPU and
   elapsed time are medians.
4. It runs it once with `SET STATISTICS XML ON` and saves the actual plan. Producing a plan costs CPU, so this run is
   not timed.

Parameters travel as real parameters, through `sp_executesql`, like the application's. Each phase has a folder in
[docs/perf/raw/](docs/perf/raw/) with the raw `SET STATISTICS` output and a `summary.csv`, and a folder in
[docs/perf/plans/](docs/perf/plans/) with `.sqlplan` files that open in SSMS.

How to read the figures:

- Physical reads were 0 in the measured runs: the data was in memory. These figures say nothing about disk speed.
- The timer has a resolution of about 15 ms, so 0 ms means "too short to see", and small differences mean nothing.
- Logical reads repeat closely, so they are the better measure. Elapsed time depends on parallelism and on the machine.
- The write tests and index rebuilds ran before the final phase, so its pages are not laid out exactly like the
  baseline's. The same query measured 20,984 reads in phase 9 and 26,651 in the final phase, on one index set.

## Test data

[sql/perf/volume-seed.sql](sql/perf/volume-seed.sql) produced 200,253 transactions, 600,222 retail lines, 50,103
shipments, 20,099 money orders and 200,251 payments. The counts and nine rule checks, all 0 violations, are in
[docs/perf/volume-seed-counts.txt](docs/perf/volume-seed-counts.txt).

The first version of the seed turned out not to be realistic. All its values came from the row number `n`, and the
rules shared factors: one branch per day, nearly every money order at BR002, money orders on few days, and no shipment or
money order ever reversed. Those correlations would have misled every plan, so branch, day and status now come from
separate bytes of a hash of `n`, and branches differ in size by 5 to 1. All five branches now sell every day.

Some tables stay small by nature: `BranchStock` has 192 rows, `Products` 32 and `Branches` 6.

## Workloads

Each workload is a real path through the application, or one of the reports in [sql/reports/](sql/reports/).

| ID | Workload | Why it matters |
| --- | --- | --- |
| W1a | `usp_DailyBranchSales`, all branches, 15 days | Auditor's daily sales report |
| W1b | The same, one branch | Manager's daily sales report |
| W1c | The same, one branch, one day | Every dashboard load |
| W2a, W2b | `/Transactions`: first page and total of a branch | The screen a branch uses most |
| W2c | `/Transactions`: first page, all branches | Auditor's list |
| W3 | `/Reports/ShippingVolume`, one branch | Monthly planning |
| W4, W4a | Money order total, one branch and all branches | Holds the non-SARGable predicate |
| W5, W5y | Top 10 products per region, 30 days and one year | Heavy, rare report |
| W6a, W6b | `usp_LowStockReport`, all branches and one branch | Every dashboard load |
| W7 | Branches with no sales on a day | Report 2 |
| R1, R4, R5 | Reports 1, 4 and 5 | Written for this phase |
| WR | 1000 sales inserted and rolled back | The write cost of each index set |

## Before and after

Before is [phase 0](docs/perf/raw/0-baseline/summary.csv): no candidate index, the first version of each query. After is
[phase 10](docs/perf/raw/10-final/summary.csv): the three kept indexes and the final queries.

| ID | Rows | Reads before | Reads after | CPU ms before | CPU ms after | Elapsed ms before | Elapsed ms after |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| W1a | 75 | 191,355 | 26,651 | 422 | 282 | 178 | 168 |
| W1b | 15 | 65,761 | 25,563 | 172 | 218 | 167 | 80 |
| W1c | 1 | 16,621 | 3,637 | 78 | 31 | 95 | 27 |
| W2a | 20 | 11,633 | 88 | 64 | 0 | 24 | 0 |
| W2b | 1 | 385 | 383 | 15 | 0 | 5 | 6 |
| W2c | 20 | 8,063 | 83 | 343 | 0 | 140 | 0 |
| W3 | 507 | 7,150 | 2,984 | 124 | 31 | 32 | 36 |
| W4 (CASE before, fixed after) | 1 | 7,802 | 1,551 | 78 | 62 | 70 | 62 |
| W5 | 50 | 23,975 | 22,967 | 344 | 359 | 106 | 109 |
| W5y | 50 | 23,975 | 23,903 | 1,593 | 953 | 1,077 | 266 |
| W6a | 24 | 52 | 52 | 0 | 0 | 0 | 0 |
| W6b | 4 | 12 | 12 | 0 | 0 | 0 | 0 |
| W7 | 0 | 8,067 | 1,147 | 63 | 47 | 21 | 46 |
| R1 | 75 | 281,748 | 29,929 | 439 | 423 | 259 | 166 |
| R4 | 30 | 9,692 | 3,740 | 78 | 63 | 85 | 69 |
| R5 | 6 | 9,463 | 2,378 | 140 | 62 | 43 | 63 |

What did not improve:

- **W7 and R5 got slower in elapsed time** (21 to 46 ms, 43 to 63 ms) while reads fell by 86 % and 75 %. With fewer reads
  the optimizer chose a serial plan instead of a parallel one, so less total work took longer on the clock. Both are
  rare reports, and CPU fell for both, so this was accepted.
- **W1b used more CPU** (172 to 218 ms) while its elapsed time halved: it is now parallel.
- **W5 and W6 did not change.** W5 must read most lines of the period, see [Scans that remain](#scans-that-remain).
- **W3 elapsed time did not change** (32 to 36 ms, within timer noise), although reads fell by 58 %.

The plans of W2a show the clearest change: [before](docs/perf/plans/0-baseline/W2a.sqlplan), a clustered index scan and a
Sort over all rows of the branch; [after](docs/perf/plans/10-final/W2a.sqlplan), a seek on
`IX_CounterTransactions_BranchCode_OpenedAt` that stops after 20 rows.

## Index candidates, step by step

Candidates were added one at a time, and all workloads ran again after each. The scripts are in
[sql/perf/candidates/](sql/perf/candidates/). Sizes are from [baseline](docs/perf/index-sizes-baseline.txt),
[all candidates](docs/perf/index-sizes-all-candidates.txt) and [final](docs/perf/index-sizes-final.txt), measured on
freshly built indexes.

| Candidate | Index | Extra MB | Main effect | Decision |
| --- | --- | ---: | --- | --- |
| C1 | `Payments (PaidAt) INCLUDE (Tax)` | 7.65 | W1a `Payments` 4,436 to 88 reads; W7 `Payments` 2,218 to 9 | **Kept** |
| C2 | `CounterTransactions (BranchCode, OpenedAt) INCLUDE (Status)`, replaces the index on `BranchCode` | 2.81 net | W2a 11,777 to 66 reads, 25 to 0 ms | **Kept** |
| C3 | `CounterTransactions (OpenedAt) INCLUDE (BranchCode, Status)` | 8.90 | W2c 5,356 to 66 reads, 93 to 0 ms | **Kept** |
| C4 | `TransactionLines (TransactionId)` covering | 14.69 | W1b halves its reads, see below | Rejected |
| C5 | `Shipments (TransactionId)` covering | 2.30 | W3 2,534 to 959 reads, 35 to 31 ms | Rejected |
| C6 | `MoneyOrders (TransactionId)` covering | 0.35 | R5 2,377 to 1,965 reads, no change in time | Rejected |
| C7 | `BranchStock (BranchCode, Quantity) WHERE Quantity <= 5` | 0.02 | None: the plan did not use it, 52 reads before and after | Rejected |

Totals: nonclustered indexes take 95.08 MB at baseline, 114.44 MB with the kept set and 131.80 MB with all seven. The
tables themselves take 219.66 MB.

**Why the covering indexes went.** With the final procedure, this is what C4 to C6 together bought
([with](docs/perf/raw/8-temp-table/summary.csv), [without](docs/perf/raw/9-no-covers/summary.csv)):

| Workload | Reads with covers | Reads without | Elapsed ms with | Elapsed ms without |
| --- | ---: | ---: | ---: | ---: |
| W1a | 10,921 | 20,984 | 204 | 141 |
| W1b | 9,747 | 19,889 | 63 | 86 |
| W1c | 1,652 | 3,632 | 27 | 30 |

Reads halve, but W1a takes longer on the clock, because the plan with covers is serial. W1b gains 23 ms and W1c 3 ms.
That does not justify 17.34 MB and 11 % more log on every sale, and a covering index also has to change whenever a report
needs another column.

**The missing-index hints were not followed blindly.** Nearly every plan still carries one, including in the final phase.
They are suggestions for a single query and take no account of write cost.

## Write overhead

[write-1000-sales.sql](sql/perf/workloads/write-1000-sales.sql) inserts 1000 sales: 1000 headers and payments, 3000
retail lines and 250 shipments. It reads the log bytes and rolls back.

The first attempt at this comparison was wrong. A rollback keeps its page splits, so each run started from a different
physical state, and one phase showed twice the log for an index on a table the test does not touch. The numbers from
phases 0 to 7 are therefore not used. [rebuild-write-tables.sql](sql/perf/rebuild-write-tables.sql) now compacts the
touched tables first, and each index set ran three times, alternating.

| Index set | Runs | Median log MB per run | CPU ms | Elapsed ms | Logical reads |
| --- | ---: | --- | --- | --- | --- |
| [Baseline](docs/perf/raw/WB-baseline-set/summary.csv) | 3 | 13.87, 13.55, 14.19 | 250, 266, 250 | 259, 261, 334 | 89,414 to 89,824 |
| [Kept three](docs/perf/raw/WF-final-set/summary.csv) | 3 | 14.37, 14.68, 14.44 | 281, 281, 297 | 305, 294, 290 | 95,571 to 95,878 |
| [With C4 to C6](docs/perf/raw/WA-all-candidates/summary.csv) | 1 | 15.98 | 297 | 296 | 97,259 |

Comparing the middle values, the kept indexes cost about 4 % more log, 12 % more CPU and 7 % more reads on writes. That is
about 0.03 ms of CPU per sale. Elapsed times overlap, so no difference there can be claimed. The first run after a
rebuild writes about 39 MB of log for every index set, because compact pages split. On a real table with free space in
its pages, the figures would lie closer to the later runs.

## Scans that remain

| Workload | Scan | Why it stays |
| --- | --- | --- |
| W6a | `BranchStock` clustered index, 4 pages | 192 rows, and it fits in 4 pages. No index beats that. This is the small-table case, not an achievement |
| W5, W5y | `TransactionLines` clustered index | A year needs 540,418 of 600,222 lines. For 30 days, the optimizer judged a scan cheaper than 45,000 seeks |
| W1a, W1b | `TransactionLines`, `Shipments`, `MoneyOrders` | Hash joins with a bitmap. W1a reads 7,535 rows of `#Paid` and keeps 22,537 of 600,222 lines |
| W2c, W7 | `IX_CounterTransactions_OpenedAt` | The narrowest index that holds what they need. W2c stops after 20 rows |
| W2b | Range seek on `IX_CounterTransactions_BranchCode_OpenedAt` | A count must visit every row of the branch; 383 pages |

## Non-SARGable predicate

`SqlReportQueries.Relevant` filtered the money order report on the payment date if there was one, and otherwise on the
opening date. EF Core turned that into a `CASE` around two columns, which no index can seek:

```sql
WHERE CASE WHEN [p].[TransactionId] IS NOT NULL THEN [p].[PaidAt] ELSE [c].[OpenedAt] END >= @start
  AND CASE WHEN [p].[TransactionId] IS NOT NULL THEN [p].[PaidAt] ELSE [c].[OpenedAt] END < @end
```

The fix makes each date a range on its own column, in two arms joined by `UNION ALL`. Their rows cannot overlap, because
one arm has a payment and the other has none:

```sql
WHERE [p].[TransactionId] IS NOT NULL AND [p].[PaidAt] >= @start AND [p].[PaidAt] < @end
UNION ALL
WHERE [p0].[TransactionId] IS NULL AND [c0].[OpenedAt] >= @start0 AND [c0].[OpenedAt] < @end0
```

Both queries were measured on the final indexes
([before SQL](docs/perf/ef-tostring-before-sarg-fix.sql), [after SQL](docs/perf/ef-tostring.sql)):

| Workload | Reads | CPU ms | Elapsed ms | Access to `Payments` | Plan |
| --- | ---: | ---: | ---: | --- | --- |
| All branches, CASE | 2,239 | 109 | 106 | Index scan, 200,251 rows | [W4a-case](docs/perf/plans/10-final/W4a-case.sqlplan) |
| All branches, fixed | 2,591 | 125 | 31 | Index seek, 8,313 rows | [W4a-sarg](docs/perf/plans/10-final/W4a-sarg.sqlplan) |
| One branch, CASE | 1,485 | 63 | 66 | Index scan, 200,250 rows | [W4-case](docs/perf/plans/10-final/W4-case.sqlplan) |
| One branch, fixed | 1,551 | 62 | 62 | Index seek, 8,313 rows | [W4-sarg](docs/perf/plans/10-final/W4-sarg.sqlplan) |

The fix replaces the scan with a seek, and the all-branch report runs in under a third of the time, because the fixed
plan goes parallel. Reads rise slightly, since two arms touch both tables, and the unpaid arm's anti-join still scans
`Payments`. For a single branch the gain is within timer noise: the `EXISTS` checks against `MoneyOrders` and
`IdentityVerifications` dominate that query. `GeneratedSqlTests` keeps the `CASE` from coming back.

## Estimated versus actual rows

**What was seen.** In [the baseline plan of W5y](docs/perf/raw/0-baseline/W5y.txt) the scan of `Payments` estimated 17,204
rows and returned 200,250, about 11.6 times more. Further up, the join estimated 30,893 rows and produced 540,418. The
memory grant of 40 MB was too small, and the hash join spilled to tempdb (`SpillToTempDb`, `HashSpillDetails`).

**Cause.** W5 and W5y run the same SQL text, so they share one cached plan, and W5 ran first. The plan itself shows it:

```text
@From  compiled '2026-10-02'  runtime '2025-11-02'
```

The optimizer estimated 30 days and was given a year. That is parameter sniffing, not stale statistics.

**Proof.** The plan cache was cleared and W5y ran first, so that it compiled for its own range
([phase E1](docs/perf/raw/E1-year-first/W5y.txt)):

| | Plan reused from 30 days | Compiled for one year |
| --- | ---: | ---: |
| `Payments` estimated | 17,204 | 200,250 |
| `Payments` actual | 200,250 | 200,250 |
| Join estimated / actual | 30,893 / 540,418 | 558,901 / 540,418 |
| Memory grant | 40,008 KB | 53,192 KB |
| Tempdb spill | Yes | No |
| Elapsed ms | 1,077 | 262 |

**Fix.** [Report 3](sql/reports/03-top-products-by-region.sql) now has `OPTION (RECOMPILE)`. It is rare and heavy, so the
compile cost is small next to the run, and the final phase has no spill in any workload.

A second, smaller mismatch has a different cause: in W5 the final aggregate estimated 4,437 groups and produced 160. The
optimizer treats region and product as independent, but 5 regions and 32 products make 160 groups at most. This changes
nothing in the plan and needs no fix.

The optimizer also used the **parameter sensitive plan** feature: the plans of `usp_DailyBranchSales` contain a
`Dispatcher` and a `QueryVariantID` at baseline, which is why W1a and W1b ran different plans from one procedure. W1c
still reused a plan compiled for 15 days. The final procedure has no dispatcher: its join works from `#Paid`, whose real
row count the optimizer can see.

## Query shape matters more than indexes here

**`usp_DailyBranchSales`.** The first version referenced one CTE twice, so the join was done twice and the optimizer stopped
with `StatementOptmEarlyAbortReason="TimeOut"`. The final version fills `#Paid` first and joins the amounts to it, so the
second statement sees real rows and real statistics.

| W1a | Reads | CPU ms | Elapsed ms |
| --- | ---: | ---: | ---: |
| CTE, no indexes (phase 0) | 191,355 | 422 | 178 |
| CTE, kept indexes (phase 3) | 177,612 | 328 | 324 |
| `#Paid`, kept indexes (phase 9) | 20,984 | 312 | 141 |

The indexes alone removed 7 % of the reads, and the rewrite another 88 %.

**Report 1.** Its first version used `OUTER APPLY` per transaction, and it is unstable:
[R1 in phase 3](docs/perf/raw/3-opened/summary.csv) took 274,940 reads, and the same query on the same indexes took
2,020,035 reads and 2,332 ms in [phase 10](docs/perf/raw/10-final/R1-apply.txt), where the plan had moved to index spools.
The likely cause is statistics refreshed by the index rebuilds; that was not isolated. The final version adds up each amount per transaction and joins once: 29,929 reads, 166 ms.

**`usp_LowStockReport`.** [sql/perf/lowstock-variants.sql](sql/perf/lowstock-variants.sql) ran 5000 calls of each shape,
half for one branch and half for all ([output](docs/perf/lowstock-variants.txt)). CPU includes compilation.

| Shape | CPU ms | Elapsed ms | µs per call | `BranchStock` reads, one branch |
| --- | ---: | ---: | ---: | ---: |
| `@Branch IS NULL OR ...`, cached plan | 4,941 | 4,958 | 991 | 4 |
| The same with `OPTION (RECOMPILE)` | 25,742 | 25,923 | 5,184 | 2 |
| Two `IF` statements (chosen) | 3,867 | 3,867 | 773 | 2 |

`OPTION (RECOMPILE)` gets the seek, but compiling costs 6.7 times the two-statement shape on a query every dashboard runs.

## Generated SQL

`GeneratedSqlMeasurements` captures SQL with `ToQueryString()` into [docs/perf/ef-tostring.sql](docs/perf/ef-tostring.sql):
the page of `/Transactions`, the shipping volume groups, the money order rows and their total. Nothing needs a server, so
`GeneratedSqlTests` asserts the shapes the indexes depend on in every test run.

What the SQL showed: `/Transactions` sorts on `OpenedAt, Id`, which led to C2; every parameter is typed `varchar(5)` or
`datetime2`, so the plans have no implicit conversions; and the money order filter held the `CASE` above.

Requests also need fewer commands: `/Reports/DailySales` went from 5 to 3, and `/Dashboard` from 6 to 4
([counts](docs/perf/ef-command-counts.txt), kept by `CommandCountTests`).

## Sale transaction

[sql/transactions/sale-transaction.sql](sql/transactions/sale-transaction.sql) is a sale in T-SQL with `BEGIN TRAN`,
`TRY...CATCH`, `SET XACT_ABORT ON`, `XACT_STATE()` and `THROW`. Its [output](docs/perf/sale-transaction-output.txt):

| Call | Failure | `XACT_STATE()` in CATCH | Stock after | Example sales after |
| --- | --- | ---: | ---: | ---: |
| Start | | | 25 | 0 |
| 1. A valid sale | None | | 24 | 1 |
| 2. Not enough stock | `THROW 51031` | -1 | 24 | 1 |
| 3. Bad payment method, on the last INSERT | CHECK, error 547 | -1 | 24 | 1 |

Call 3 fails after the stock, header and line were written, and nothing remains. With `XACT_ABORT ON` both failures doom
the transaction, even the deliberate `THROW`, so only `ROLLBACK` is allowed. The caller receives the original error.

## Blocking and isolation

Session A updates a stock row and waits 8 seconds before rolling back. Session B reads that row, then updates it, and a
third session watches. Scripts are in [sql/transactions/](sql/transactions/), output in
[docs/perf/blocking/](docs/perf/blocking/). The sessions ran in **sqlcmd, not SSMS**, because SSMS is not installed; the
scripts work unchanged in two SSMS windows.

| Database setting | Isolation reported by B | B's read | B's update | Wait seen by the monitor |
| --- | --- | ---: | ---: | --- |
| `READ_COMMITTED_SNAPSHOT` off (as deployed) | READ COMMITTED | 6,357 ms | 1 ms | `LCK_M_S`, session 55 blocked by 51 |
| `READ_COMMITTED_SNAPSHOT` on (perf database, test only) | READ COMMITTED | 0 ms | 6,429 ms | `LCK_M_U`, session 106 blocked by 51 |

**Observed behaviour.** With the setting off, READ COMMITTED uses locks. A holds an exclusive (`X`) lock on the row key and
intention locks above it; B asks for a shared (`S`) lock, and its status stays `WAIT` until A ends. B then reads 33, the
committed value, and its update finds no lock left. B never sees A's uncommitted change.

With the setting on, the read uses row versions from tempdb. It does not wait and returns the same committed 33. A writer
still needs the row, so the update waits instead, on an update (`U`) lock. Row versioning removes reader and writer
conflicts, not writer and writer conflicts.

The setting stays off: changing it adds tempdb load, which was not measured, and no load test has shown a blocking
problem yet. The application keeps its transactions short. This is the first thing to try if reports start to wait on
sales.

## Least-privilege database role

[sql/security/least-privilege-role.sql](sql/security/least-privilege-role.sql) defines the role `apexpost_app`, and
[create-app-login.sql](sql/security/create-app-login.sql) creates its login. The password is passed in, never stored.
Migrations run under a separate deployment login with `db_owner`.

| Object | Permission | Why |
| --- | --- | --- |
| All 17 application tables | SELECT | Screens, reports and query services |
| `Branches`, `Products` | UPDATE | Open and close branches, revise prices |
| `BranchStock` | UPDATE | A sale or reversal changes the quantity |
| `Counters` | INSERT, DELETE | Add and remove counters |
| `Staff`, `CounterTransactions` | INSERT, UPDATE | Staff administration, the transaction life cycle |
| `TransactionLines`, `Shipments`, `MoneyOrders`, `Payments`, `IdentityVerifications` | INSERT | Nothing edits or removes a line, payment or check |
| `AuditEntries` | INSERT (SELECT above), DENY UPDATE, DELETE | Append-only |
| `StockAdjustments` | SELECT, DENY INSERT, UPDATE, DELETE | Only `usp_AdjustStock` writes history |
| The three procedures | EXECUTE | Ownership chaining lets `usp_AdjustStock` insert without a table grant |

The login gets no `db_datareader`, `db_datawriter` or `db_owner`, no DDL, and no access to `__EFMigrationsHistory`,
because the application never migrates. Development seeding needs more, so it runs under the owner.

`LeastPrivilegeTests` runs the real application on this role: a sale with all three line kinds, a refund, a stock
adjustment, branch and staff administration, all reports and the audit log. It also proves that 9 forbidden statements
fail. [docs/perf/least-privilege-check.txt](docs/perf/least-privilege-check.txt) shows the effective permissions and the
same refusals (error 229) on the performance database.

## Index Catalogue

The keys, included columns and filters are read from the database: [docs/perf/schema-checks.txt](docs/perf/schema-checks.txt).
Primary keys are left out. Paths are under `src/ApexPost.Infrastructure/Persistence/`. "FK" means EF Core created the
index for that foreign key by convention.

| Index | Table | Keys | Include | Filter | Declared in (file:line) | Migration |
| --- | --- | --- | --- | --- | --- | --- |
| `IX_CounterTransactions_BranchCode_OpenedAt` | CounterTransactions | BranchCode, OpenedAt | Status | - | Configurations/CounterTransactionConfiguration.cs:36 | AddWorkloadIndexes |
| `IX_CounterTransactions_OpenedAt` | CounterTransactions | OpenedAt | BranchCode, Status | - | Configurations/CounterTransactionConfiguration.cs:39 | AddWorkloadIndexes |
| `IX_Payments_PaidAt` | Payments | PaidAt | Tax | - | Configurations/CounterTransactionConfiguration.cs:76 | AddWorkloadIndexes |
| `IX_IdentityVerifications_TransactionId` | IdentityVerifications | TransactionId | - | - | Records/IdentityVerificationRecord.cs:7 | InitialCreate |
| `IX_StockAdjustments_BranchCode` | StockAdjustments | BranchCode | - | - | Records/StockAdjustmentRecord.cs:8 | InitialCreate |
| `IX_StockAdjustments_Sku` | StockAdjustments | Sku | - | - | Records/StockAdjustmentRecord.cs:9 | InitialCreate |
| `IX_StockAdjustments_StaffId` | StockAdjustments | StaffId | - | - | Records/StockAdjustmentRecord.cs:10 | InitialCreate |
| `UX_Counters_Branch_Number` | Counters | BranchCode, Number (unique) | - | `[BranchCode] IS NOT NULL` | Configurations/CounterConfiguration.cs:16 | InitialCreate |
| `UX_Staff_Email` | Staff | Email (unique) | - | - | Configurations/StaffConfiguration.cs:35 | InitialCreate |
| `IX_BranchStock_Sku` | BranchStock | Sku | - | - | FK, Configurations/BranchStockConfiguration.cs:26 | InitialCreate |
| `IX_CounterTransactions_ClerkId` | CounterTransactions | ClerkId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:47 | InitialCreate |
| `IX_CounterTransactions_CounterId` | CounterTransactions | CounterId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:45 | InitialCreate |
| `IX_CounterTransactions_ReversalManagerId` | CounterTransactions | ReversalManagerId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:91 | InitialCreate |
| `IX_MoneyOrders_TransactionId` | MoneyOrders | TransactionId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:50 | InitialCreate |
| `IX_Products_Category` | Products | Category | - | - | FK, Configurations/ProductConfiguration.cs:32 | InitialCreate |
| `IX_Shipments_TransactionId` | Shipments | TransactionId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:50 | InitialCreate |
| `IX_Shipments_ZoneCode` | Shipments | ZoneCode | - | - | FK, Configurations/ShipmentConfiguration.cs:32 | InitialCreate |
| `IX_Staff_HomeBranch` | Staff | HomeBranch | - | - | FK, Configurations/StaffConfiguration.cs:36 | InitialCreate |
| `IX_Staff_Role` | Staff | Role | - | - | FK, Configurations/StaffConfiguration.cs:37 | InitialCreate |
| `IX_TransactionLines_Sku` | TransactionLines | Sku | - | - | FK, Configurations/RetailLineConfiguration.cs:27 | InitialCreate |
| `IX_TransactionLines_TransactionId` | TransactionLines | TransactionId | - | - | FK, Configurations/CounterTransactionConfiguration.cs:50 | InitialCreate |

Three of these came from a workload; `IX_CounterTransactions_BranchCode_OpenedAt` also replaced the foreign key index on
`BranchCode`. The rest are the foreign key and uniqueness indexes that existed already. Foreign key indexes such as
`IX_CounterTransactions_ClerkId` (11 MB) were not used by any measured workload. They stay because they support the
`Restrict` checks when a parent row is deleted; removing them was not measured.

## Verification status

| Requirement | Status | Evidence |
| --- | --- | --- |
| Reporting queries executed | Verified | [report-outputs.txt](docs/perf/report-outputs.txt) |
| Procedures: valid calls and every `THROW` | Verified | [procedure-checks.txt](docs/perf/procedure-checks.txt) |
| Procedures called through parameters from EF Core | Verified | `StoredProcedureTests`, `SqlReportQueries`, `SqlStockAdjuster` |
| Migration, also as an idempotent script run twice on a blank database | Verified | 3 procedures, 5 migrations, 3 indexes afterwards |
| Sale transaction and rollback | Verified | [sale-transaction-output.txt](docs/perf/sale-transaction-output.txt) |
| Plans, reads and CPU before and after | Verified | [docs/perf/raw/](docs/perf/raw/), [docs/perf/plans/](docs/perf/plans/) |
| `ToQueryString()` | Verified | [ef-tostring.sql](docs/perf/ef-tostring.sql) |
| Delete behaviour and constraints | Verified | [schema-checks.txt](docs/perf/schema-checks.txt) |

### Not verified

- **Two SSMS sessions.** SSMS is not installed on the test machine. Blocking was observed in two sqlcmd sessions with a
  third as monitor. The scripts and the isolation behaviour are the same, but SSMS itself was not used.
- **Screenshots.** There are none. The evidence is `.sqlplan` files and raw text output.
- **Signing in with the application login.** This server accepts Windows logins only (`IsIntegratedSecurityOnly = 1`), so
  `apexpost_app_login` could be created but not used to connect. The role was tested by impersonating a database user,
  which checks the same permissions.
- **Disk performance.** All runs were served from memory.
- **Load.** No test ran many users at once, so lock contention under real traffic is unknown.
- **CI.** The pipeline uses a SQL Server 2022 image. These measurements come from SQL Server 2025 and were not repeated
  there; the tests were not run on 2022.
- **Week 3 lab brief.** `docs/lab/week3-brief.md` was not available, so this work follows `CLAUDE.md` and the task
  description.
