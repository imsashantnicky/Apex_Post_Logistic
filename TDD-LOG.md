# TDD log – scan age rule (Week 5)

This log records one rule built test-first, in small RED / GREEN / REFACTOR commits. Each commit was pushed on its
own, so the history on `main` shows the order.

## 1. Requirement

> The server rejects an uploaded scan whose `scannedAtUtc` is more than 72 hours before the upload time, with the
> reason "Scan is older than 72 hours.". A scan exactly 72 hours old is accepted. The limit is configurable
> (`TrackingOptions.MaxScanAgeHours`, default 72).

Why: a courier device can stay offline for a long time. A scan that arrives days late would move a shipment's status
long after the parcel has moved on, so the server refuses it with a reason and the device marks it `Failed`.

### Proof that the rule did not exist

Before the first test, the code was searched for the rule:

```text
grep -rniE "72|older than|MaxScanAge|ScanAge|TrackingOptions" --include=*.cs src
```

The only hits were GUID examples containing "72" in scan ids (`ContractRules.cs`, `ScanItem.cs`,
`ScanResponse.cs`, `ScanUploadRequest.cs`). There was no age check in `ShipmentTrackingService`, no
`TrackingOptions` class and no `MaxScanAgeHours` setting. An upload accepted a scan of any age.

## 2. First failing test (RED)

File: `tests/ApexPost.Tracking.UnitTests/Services/ScanAgeRuleTests.cs`. The upload time is fixed with a
`FakeTimeProvider` (2026-11-10 12:00 UTC); the scan was made at 2026-11-07 11:59 UTC, which is 72 hours and
1 minute earlier.

```csharp
[Fact]
public async Task UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason()
{
    // Arrange
    _store.Store(TestData.Shipment());
    var scan = ScanAt(new DateTime(2026, 11, 7, 11, 59, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.UploadScansAsync([scan], Ct);

    // Assert
    var rejected = Assert.Single(result.Rejected);
    Assert.Equal(scan.ScanId, rejected.ScanId);
    Assert.Equal("Scan is older than 72 hours.", rejected.Reason);
    Assert.Equal(0, result.Accepted);
    Assert.Empty(_store.StoredScans);
}
```

The test compiles and fails on an assertion (not a compile error), because the service accepted the old scan.
Real output of
`dotnet test tests/ApexPost.Tracking.UnitTests -c Release --filter "FullyQualifiedName~ScanAgeRuleTests"`:

```text
[xUnit.net 00:00:01.53]     ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason [FAIL]
  Failed ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason [44 ms]
  Error Message:
   Assert.Single() Failure: The collection was empty
  Stack Trace:
     at ApexPost.Tracking.UnitTests.Services.ScanAgeRuleTests.UploadScansAsync_ScanOlderThan72Hours_RejectsScanWithReason() in e:\week-3-assignment\Apex_Post_Logistic\tests\ApexPost.Tracking.UnitTests\Services\ScanAgeRuleTests.cs:line 39
--- End of stack trace from previous location ---

Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 83 ms - ApexPost.Tracking.UnitTests.dll (net10.0)
```
