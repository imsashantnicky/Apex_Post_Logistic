# Copilot log – ApexPost Logistics (Week 5)

GitHub Copilot was used by the **user** to draft tests. Claude Code wrote the prompts and helped review and edit the
output. It did not write Copilot's answers, and the raw answers are saved unchanged. The user confirmed every
kept / changed / rejected decision below before any edit was applied.

- Prompts: `docs/copilot/PROMPTS.md`
- Raw, unedited answers: `docs/copilot/raw/`
- Mutation proof output: `docs/copilot/evidence/`
- Every Copilot-assisted class starts with:
  `// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).`
  (the feature file starts with the same line as a `#` comment).

Decision values: **Kept** (used as generated), **Changed** (used after an edit; the edit is described),
**Rejected** (deleted; the reason is given). Duplicates of existing tests were deleted, not rewritten into new tests.

## Prompt A – unit tests for `ShipmentTrackingService`

| Item | Value |
| --- | --- |
| Tool and model | GitHub Copilot Chat in <TOOL>, Ask mode; model <MODEL> |
| Date | <DATE> |
| Context files | As listed in `docs/copilot/PROMPTS.md`, prompt A |
| Raw answer | `docs/copilot/raw/prompt-a-shipment-tracking-service-tests.md` |
| Final test file | `tests/ApexPost.Tracking.UnitTests/Services/ShipmentTrackingServiceCopilotTests.cs` |

### Prompt text

The exact text of prompt A, copied from `docs/copilot/PROMPTS.md`:

```text
You are helping me write unit tests for a .NET 10 / C# 14 project.

Class under test: ShipmentTrackingService in #file:src/ApexPost.Application/Tracking/ShipmentTrackingService.cs
Ports: #file:src/ApexPost.Application/Tracking/ITrackedShipmentRepository.cs
#file:src/ApexPost.Application/Tracking/ITrackingQueries.cs
#file:src/ApexPost.Application/Tracking/ITrackingUnitOfWork.cs
#file:src/ApexPost.Application/Tracking/ICurrentCourier.cs
Rules: #file:src/ApexPost.Application/Tracking/ScanAgePolicy.cs
#file:src/ApexPost.Application/Tracking/ScanUpload.cs
#file:src/ApexPost.Domain/Tracking/ShipmentStatusRules.cs
Test data helpers: #file:tests/ApexPost.Tracking.UnitTests/Support/TestData.cs

Write one test class ShipmentTrackingServiceCopilotTests in namespace ApexPost.Tracking.UnitTests.Services
for the methods UploadScansAsync and ChangeStatusAsync only.

Technical rules:
- xUnit v3 (package xunit.v3). Use TestContext.Current.CancellationToken as the token. No NUnit, no FluentAssertions,
  no NSubstitute, no AutoFixture.
- Moq for ITrackedShipmentRepository, ITrackingQueries, ITrackingUnitOfWork and ICurrentCourier.
  Use Setup, ReturnsAsync, ThrowsAsync, It.Is<>, It.IsAny<> and Verify with Times.Once / Times.Never.
- Microsoft.Extensions.Time.Testing.FakeTimeProvider for the clock, starting at a fixed instant
  (2026-11-05 09:00 UTC). Never use DateTime.Now, DateTime.UtcNow or DateTimeOffset.UtcNow.
- Build the real ScanAgePolicy with Options.Create(new TrackingOptions { MaxScanAgeHours = 72 }).
- Fictional data only: tracking numbers like APX-10000001, courier ids CR-101 and CR-102,
  scan ids "scan::" followed by a Guid.
- Every test has // Arrange, // Act, // Assert comments.
- Test names follow Method_Scenario_ExpectedResult, for example
  UploadScansAsync_ScanFromOtherCourier_ThrowsForbiddenException.
- Put [Trait("Category", "Unit")] on the class.
- Use [Theory] with [InlineData] or TheoryData<> where a rule has several cases.
- For exceptions, assert the exact exception type and the exact message.

Cases to cover for UploadScansAsync:
1. Valid Pickup scan for a Created shipment: accepted = 1, the scan is staged with AddScan, the shipment moves
   to PickedUp, SaveChangesAsync is called once.
2. Scan id already stored (FindStoredScanIdsAsync returns it): counted as duplicate, AddScan and SaveChangesAsync
   never called.
3. The same scan id twice in one batch: accepted 1, duplicates skipped 1.
4. Unknown tracking number: rejected with reason "Unknown tracking number.", nothing saved.
5. Scan age boundary as a Theory: 71 hours and exactly 72 hours accepted, 72 hours plus 1 second and 73 hours
   rejected with reason "Scan is older than 72 hours.".
6. Backward scan (Pickup for a shipment already InTransit) and any scan after Delivered: rejected with a reason.
7. Caller is not a courier (CourierId null) or a scan carries another courier's id: ForbiddenException,
   and the repository is never called.
8. Batch of 0 scans and of 101 scans: the exception the service throws for the size limit.
9. SaveChangesAsync throws DuplicateKeyException the first time: DiscardChanges is called once and the upload
   is tried again.

Cases to cover for ChangeStatusAsync:
1. Every allowed forward move (for example Created -> PickedUp, InTransit -> Delivered): status changes,
   SaveChangesAsync called once, the view comes from ITrackingQueries.FindAsync.
2. Backward move and same-status move (for example InTransit -> PickedUp, PickedUp -> PickedUp):
   InvalidStatusTransitionException with the exact message, SaveChangesAsync never called.
3. Any move from Delivered or Failed: InvalidStatusTransitionException.
4. Unknown tracking number: NotFoundException "Shipment APX-10000009 does not exist.".
5. A status value that is not defined in the enum, for example (ShipmentStatus)99: ValidationFailedException.
6. The cancellation token is passed on to the repository and to SaveChangesAsync.

Return only the complete C# file, with the using directives it needs. Do not change production code.
After the code, list any case you could not write and say why.
```

### What Copilot produced

- One class with **16 test methods (39 executed cases)**: 10 methods for `UploadScansAsync`, 6 for
  `ChangeStatusAsync`. Copilot first read the class under test, the ports, the existing `ShipmentTrackingServiceMoqTests`,
  `ScanAgeRuleTests` and `ScanOwnershipTests` (its tool trace is in the raw file).
- **Covered:** all 9 upload cases and all 6 status cases asked for. It also wrote the full forward matrix
  (14 allowed moves) and checked exact reason texts and exception messages.
- **Missed:** no scan after `Failed` in the backward-scan theory (it used only `Delivered`); only `null` for "not a
  courier" (not `""` or a malformed id). It said "Cases not written: None".
- **Wrong:** one line did not compile (`Assert.Empty(result.DuplicatesSkipped == 0 ? [] : [result.DuplicatesSkipped])`,
  CS1503). The retry test did not verify the second `SaveChangesAsync`.
- **Duplicates:** although it had read `ShipmentTrackingServiceMoqTests`, 7 methods repeat tests that already exist.
- **Nothing invented:** every API, message and exception it used exists; data is fictional (`APX-10000001`, `CR-101`,
  `Mia Example`, an Ofcom drama number `+44 20 7946 0001`).
- After the one-line compile fix, all 39 cases passed on the first run.

### Decisions

| # | Generated test | Decision | Reason / edit made |
| --- | --- | --- | --- |
| A1 | `UploadScansAsync_ValidPickupScan_AcceptsAndStagesScan` | Kept | Checks the move to PickedUp, the staged scan's type and courier, and the caller's token reaching `SaveChangesAsync`; the Moq test `NewScans` does not check the status |
| A2 | `UploadScansAsync_ScanIdAlreadyStored_CountsDuplicateWithoutSaving` | Rejected | Duplicate of `ShipmentTrackingServiceMoqTests.UploadScansAsync_EveryScanAlreadyStored_NeverLoadsShipmentsAndNeverSaves` (same setup, same `Times.Never` checks) |
| A3 | `UploadScansAsync_SameScanIdTwiceInBatch_AcceptsOnceAndCountsDuplicate` | Rejected | Duplicate of `ShipmentTrackingServiceMoqTests.UploadScansAsync_SameScanIdTwiceInBatch_StagesItOnceAndCountsOneDuplicate`, which also checks the ids sent to `FindStoredScanIdsAsync` |
| A4 | `UploadScansAsync_UnknownTrackingNumber_RejectsWithoutSaving` | Changed | The duplicate check did not compile and was roundabout. Edit: `Assert.Empty(result.DuplicatesSkipped == 0 ? [] : [result.DuplicatesSkipped]);` → `Assert.Equal(0, result.DuplicatesSkipped);`. No Moq test covered this case |
| A5 | `UploadScansAsync_ScanAgeBoundary_AcceptsWithinLimitAndRejectsOlder` (71 h, 72 h, 72 h + 1 s, 73 h) | Kept | Adds 71 h and 73 h to `ScanAgeRuleTests` and verifies staging and saving; the 72 h case is the one mutation M1 breaks. The if/else in the assertion is a style smell, not a defect |
| A6 | `UploadScansAsync_BackwardOrAfterDelivered_RejectsWithReason` (5 cases) | Kept | Asserts the exact reason text; the existing in-memory tests use only `Contains` or `Single`. After-`Failed` is covered by `No_scan_is_accepted_after_Delivered_or_Failed` |
| A7 | `UploadScansAsync_CallerIsNotCourier_ThrowsForbiddenExceptionBeforeRepositoryCalls` | Rejected | Weaker duplicate of `ShipmentTrackingServiceMoqTests.UploadScansAsync_CallerIsNotACourier_…` (that theory covers `null`, `""` and `"CR-1"`) |
| A8 | `UploadScansAsync_ScanBelongsToAnotherCourier_ThrowsForbiddenExceptionBeforeRepositoryCalls` | Rejected | Duplicate of `ShipmentTrackingServiceMoqTests.UploadScansAsync_ScanOfAnotherCourier_…`, which is stronger (mixed batch, `VerifyNoOtherCalls`). The now-unused constant `OtherCourierId` and helper `VerifyRepositoryWasNotCalled()` were deleted with it |
| A9 | `UploadScansAsync_InvalidBatchSize_ThrowsValidationFailedException` (0, 101) | Kept | The existing in-memory test checks only the exception type; this one checks the message, the field and text, and that the repository is never called |
| A10 | `UploadScansAsync_SaveThrowsDuplicateKey_DiscardsAndRetriesUpload` | Changed | A different path from the Moq test (the retry reloads, stages again and accepts), but the second save was never verified. Edit: added `_unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));` |
| A11 | `ChangeStatusAsync_AllowedForwardMove_UpdatesAndReturnsQueryView` (14 cases) | Kept | Every allowed move at service level, and the result is the view from `ITrackingQueries`; existing service tests cover only Created → PickedUp |
| A12 | `ChangeStatusAsync_BackwardOrSameMove_ThrowsInvalidStatusTransitionException` (2 cases) | Rejected | Duplicate of `ShipmentTrackingServiceMoqTests.ChangeStatusAsync_InvalidTransition_…` (InTransit → PickedUp, InTransit → InTransit), which also checks `From` and `To` |
| A13 | `ChangeStatusAsync_MoveFromFinalStatus_ThrowsInvalidStatusTransitionException` (2 cases) | Rejected | Both cases exist: Delivered → Failed in `ShipmentTrackingServiceMoqTests`, Failed → Delivered in `ShipmentTrackingServiceTests` |
| A14 | `ChangeStatusAsync_UnknownTrackingNumber_ThrowsNotFoundException` | Rejected | Duplicate of `ShipmentTrackingServiceMoqTests.ChangeStatusAsync_UnknownTrackingNumber_ThrowsNotFoundExceptionAndNeverSaves` |
| A15 | `ChangeStatusAsync_UndefinedStatus_ThrowsValidationFailedException` | Kept | Overlaps the in-memory test but adds what only a mock can prove: `FindAsync` is never called, so validation runs before any read |
| A16 | `ChangeStatusAsync_CallerToken_IsPassedToRepositoryAndUnitOfWork` | Kept | No existing token test covers `ChangeStatusAsync` |

Final class: **9 methods, 30 executed cases**, all passing.

### Check against the Week 5 test rules

| Rule (TESTING-STRATEGY.md section 7) | Met by the generated code? | Fix made |
| --- | --- | --- |
| AAA comments | Yes, in every test | None |
| `Method_Scenario_ExpectedResult` names | Yes | None |
| `[Trait("Category", "Unit")]` | Yes, on the class | None |
| `FakeTimeProvider`, no `DateTime.UtcNow` | Yes, fixed at 2026-11-05 09:00 UTC; times derived from it | None |
| Exact exception type and message | Yes (`ThrowsAsync<T>` + exact `Message`, field and text for validation) | None |
| Fictional data only | Yes | None |
| Overlap with `ShipmentTrackingServiceMoqTests` | No: 7 of 16 methods were duplicates | Deleted (A2, A3, A7, A8, A12, A13, A14) |
| Compiles with 0 warnings | No: CS1503 on one line | Fixed (A4) |

## Prompt B – `ShipmentStatusUpdate.feature` with Reqnroll steps

| Item | Value |
| --- | --- |
| Tool and model | GitHub Copilot Chat in <TOOL>, Ask mode; model <MODEL> |
| Date | <DATE> |
| Context files | As listed in `docs/copilot/PROMPTS.md`, prompt B |
| Raw answer | `docs/copilot/raw/prompt-b-shipment-status-update-feature.md` |
| Final files | `tests/ApexPost.Specs/Features/ShipmentStatusUpdate.feature`, `tests/ApexPost.Specs/Contexts/ShipmentStatusContext.cs`, `tests/ApexPost.Specs/Steps/ShipmentStatusUpdateSteps.cs` |

### Prompt text

The exact text of prompt B, copied from `docs/copilot/PROMPTS.md`:

```text
You are helping me write a BDD specification with Reqnroll 3 on xUnit v3 for a .NET 10 / C# 14 project.

Style examples to follow exactly:
#file:tests/ApexPost.Specs/Features/ScanSync.feature
#file:tests/ApexPost.Specs/Steps/ScanSyncSteps.cs
#file:tests/ApexPost.Specs/Contexts/ScanSyncContext.cs
#file:tests/ApexPost.Specs/Hooks/ScenarioHooks.cs
Code to use: #file:tests/ApexPost.Specs/Support/SpecCourier.cs
#file:tests/ApexPost.Tracking.UnitTests/Support/InMemoryTracking.cs
#file:src/ApexPost.Application/Tracking/ShipmentTrackingService.cs
#file:src/ApexPost.Domain/Tracking/ShipmentStatusRules.cs
#file:src/ApexPost.Application/Tracking/InvalidStatusTransitionException.cs

Write three files:

1. Features/ShipmentStatusUpdate.feature
   - Tag the feature @Spec. Feature name: "Shipment status update".
   - A short description in business language: a dispatcher moves a shipment through its journey; a shipment only
     moves forward and never changes after it is delivered or failed.
   - A Background that registers shipment APX-40000001 for a recipient in Harbour Point.
   - Scenario: a shipment is moved forward step by step (Created -> Picked Up -> In Transit -> Out For Delivery ->
     Delivered) and has status Delivered at the end.
   - Scenario: an invalid backward move (from In Transit back to Picked Up) is refused, the dispatcher is told the
     shipment only moves forward, and the status stays In Transit.
   - Scenario: a delivered shipment cannot change any more.
   - Scenario: changing the status of an unknown shipment is refused as not found.
   - One Scenario Outline with Examples for at least 4 from/to pairs, with a column saying whether the change is
     allowed or refused (include one skip forward, one backward, one same status, one from Failed).
   - Business language only: no URLs, no JSON, no HTTP codes, no class or method names in the feature file.

2. Contexts/ShipmentStatusContext.cs
   - A per-scenario context class (Reqnroll creates one per scenario and injects it), no static mutable fields.
   - Holds a FakeTimeProvider at a fixed instant 2026-11-05 08:00 UTC, an InMemoryTracking server, the last
     ShipmentView seen and the last exception caught.
   - XML doc comments on every public member.

3. Steps/ShipmentStatusUpdateSteps.cs
   - [Binding] class with the context injected through the primary constructor.
   - Service level, no HTTP: build the real ShipmentTrackingService with the InMemoryTracking instance as
     repository, queries and unit of work, new SpecCourier("CR-101"), the FakeTimeProvider and
     new ScanAgePolicy(Options.Create(new TrackingOptions())).
   - Store the Background shipment with TrackedShipment.Create and InMemoryTracking.Store.
   - Catch InvalidStatusTransitionException and NotFoundException in the When step and assert in the Then steps.
   - Convert the business status names ("Picked Up", "In Transit", "Out For Delivery") to ShipmentStatus in one place.
   - Use TestContext.Current.CancellationToken. No DateTime.Now or DateTime.UtcNow. No static fields.
   - Plain xUnit Assert only, no FluentAssertions.
   - XML doc comments on the class and on public methods.

Fictional data only (APX-4000000x, CR-101, names like "Ava Example").
Return the three complete files, each in its own code block with its path as a heading.
After the code, list any step text that could match two bindings, and any assumption you made.
```

### What Copilot produced

- Three files: a feature with a Background, 4 scenarios and 1 Scenario Outline with 4 examples (**8 executed
  cases**), a per-scenario context class and a binding class with 8 step definitions.
- Business language only, `@Spec` tag, context injection through the primary constructor, no static mutable fields,
  `FakeTimeProvider` at 2026-11-05 08:00 UTC, `TestContext.Current.CancellationToken`, plain xUnit asserts.
- The outline has the four pairs asked for: skip forward (Created → In Transit), backward, same status, from Failed.
- **Wrong:** it said "Potentially ambiguous step text: None", but `Then shipment {string} has status {string}` is
  identical to a binding in `ScanSyncSteps`. The project compiled, but **7 of 8 scenarios failed** with
  `Reqnroll.AmbiguousBindingException`.
- **Nothing invented:** `InMemoryTracking.Store` / `Stored`, `SpecCourier` and every message exist.

### Decisions

| # | Generated scenario / binding | Decision | Reason / edit made |
| --- | --- | --- | --- |
| B1 | Feature, Background and "A shipment moves forward step by step" | Kept | Business language, `@Spec`, Background registers the shipment |
| B2 | Scenario "A backward move is refused" | Kept | Exact refusal and the status stays In Transit; the scenario mutation M2 breaks |
| B3 | Scenario "A delivered shipment cannot change any more" | Kept | The exact message ("from Delivered to Failed") is specific enough |
| B4 | Scenario "An unknown shipment cannot be changed" | Kept | Exact not-found message |
| B5 | Scenario Outline "A status change follows the shipment journey" (4 examples) | Kept | Meets the prompt (skip forward, backward, same status, from Failed) |
| B6 | `Contexts/ShipmentStatusContext.cs` | Kept | One instance per scenario, fixed fake clock, no static state, XML docs |
| B7 | `Steps/ShipmentStatusUpdateSteps.cs` | Changed | The unscoped `Then shipment {string} has status {string}` clashed with `ScanSyncSteps`. Edit: added `[Scope(Feature = "Shipment status update")]` under `[Binding]`; Reqnroll then prefers the scoped binding in this feature and ignores it elsewhere. Result: all 20 spec cases pass |

### Check against the Week 5 BDD rules

| Rule (TESTING-STRATEGY.md section 9) | Met by the generated code? | Fix made |
| --- | --- | --- |
| Business language only | Yes (no URLs, JSON, codes or class names in the feature) | None |
| Background and Scenario Outline with Examples | Yes | None |
| Context injection, no static fields | Yes | None |
| `@Spec` tag (`Category=Spec`) | Yes | None |
| Service level stated (real service, in-memory ports) | Yes: real `ShipmentTrackingService` on `InMemoryTracking` | None |
| No step text clashes with existing bindings | No: one clash with `ScanSyncSteps` | Feature scope on the binding class (B7) |

## Mutation proofs

Each proof breaks production code on purpose, runs the Copilot-assisted tests, restores the code with `git restore`
and runs them again. After both proofs `git diff -- src/` is empty; the broken code was never committed.
Machine names and personal paths in the output are replaced with `<path>`.

| # | Production file:line | Change (before → after) | Test that failed | Failing output | Passing output |
| --- | --- | --- | --- | --- | --- |
| M1 | `src/ApexPost.Application/Tracking/ScanAgePolicy.cs:25` | `uploadedAtUtc - scannedAtUtc > _maxAge` → `>= _maxAge` | `ShipmentTrackingServiceCopilotTests.UploadScansAsync_ScanAgeBoundary_AcceptsWithinLimitAndRejectsOlder(hoursOld: 72, extraSecondsOld: 0, accepted: True)`: `Assert.Equal() Failure`, Expected 1, Actual 0 (29 passed, 1 failed) | `docs/copilot/evidence/mutation-01-scan-age-boundary-failing.txt` | `docs/copilot/evidence/mutation-01-scan-age-boundary-passing.txt` (30 passed) |
| M2 | `src/ApexPost.Domain/Tracking/ShipmentStatusRules.cs:18` | `Stage(to) > Stage(from)` → `Stage(to) != Stage(from)` | "A backward move is refused" and the outline row In Transit → Picked Up: `Assert.IsType() Failure: Value is null`, expected `InvalidStatusTransitionException` (6 passed, 2 failed) | `docs/copilot/evidence/mutation-02-backward-move-failing.txt` | `docs/copilot/evidence/mutation-02-backward-move-passing.txt` (8 passed) |

Both failures are assertion failures, not compile errors. M1 shows the boundary case matters: only "exactly 72 hours"
fails, so a test set without it would not notice the mutation.

## Summary

| | Prompt A | Prompt B |
| --- | --- | --- |
| Generated tests / scenarios | 16 methods, 39 cases | 5 scenarios (1 outline), 8 cases; 2 support files |
| Kept | 7 methods (28 cases) | 5 scenarios + context class |
| Changed | 2 methods (2 cases) | 1 file (step bindings scoped) |
| Rejected | 7 methods (9 cases) | 0 |
| Final | 9 methods, 30 cases, all passing | 8 cases, all passing |

Full suite after the step: `dotnet test Apex_Post_Logistic.slnx -c Release --filter "Category!=Measurement"`:
1,979 passed, 0 failed, 0 skipped; build 0 warnings, 0 errors.

## What Copilot did well and where it needed a human

Copilot read the right files, followed the house style (AAA, naming, trait, `FakeTimeProvider`, fictional data) and
produced exact messages and a complete forward-move matrix that were correct on the first run. Its boundary theory
included the "exactly 72 hours" case, which mutation M1 proved is the one case that catches an off-by-one.
It needed a human in three places: one line did not compile, almost half of the unit tests repeated tests it had
just read, and it claimed no step text was ambiguous while one step clashed with an existing binding and broke 7 of 8
scenarios. Its own "Cases not written: None" and "ambiguous: None" notes were therefore not reliable; only building,
running and mutating the tests showed what they were worth.
