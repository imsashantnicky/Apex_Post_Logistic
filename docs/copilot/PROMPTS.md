# GitHub Copilot prompts (Week 5, step 09)

These two prompts are run by the **user** in GitHub Copilot Chat. Claude wrote the prompts only; it does not write
or edit what Copilot answers. The answers are saved unchanged in `docs/copilot/raw/` and reviewed in
`COPILOT-LOG.md` (kept / changed / rejected, mutation proofs).

## How to run a prompt

1. Open the repository in VS Code (or Visual Studio 2026) on branch `week5-testing-09-copilot`.
2. Open Copilot Chat in **Ask** mode (not Agent or Edit mode, so Copilot answers in the chat and changes no file).
   Note the model shown in the model picker; it goes into `COPILOT-LOG.md`.
3. Start a **new chat** for each prompt, so prompt B does not see the answer to prompt A.
4. Open the context files listed under the prompt in editor tabs. The prompt text also names them with `#file:`,
   which attaches them in VS Code. In Visual Studio, type `#` and pick each file from the list instead.
5. Paste the prompt text exactly as written (everything inside the grey block) and send it.
6. Copy the whole answer with the chat's copy button and save it unchanged (see "Where to save the output").
   If Copilot stops halfway, send `continue` once and append that answer to the same raw file under a line
   `--- continue ---`.

## Prompt A – unit tests for `ShipmentTrackingService`

**Context files to open:**

| File | Why Copilot needs it |
| --- | --- |
| `src/ApexPost.Application/Tracking/ShipmentTrackingService.cs` | The class under test |
| `src/ApexPost.Application/Tracking/ITrackedShipmentRepository.cs` | Port to mock |
| `src/ApexPost.Application/Tracking/ITrackingQueries.cs` | Port to mock |
| `src/ApexPost.Application/Tracking/ITrackingUnitOfWork.cs` | Port to mock |
| `src/ApexPost.Application/Tracking/ICurrentCourier.cs` | Port to mock |
| `src/ApexPost.Application/Tracking/ScanAgePolicy.cs` | Real scan age rule (72 hours) |
| `src/ApexPost.Application/Tracking/ScanUpload.cs` | Input of an upload |
| `src/ApexPost.Domain/Tracking/ShipmentStatusRules.cs` | Forward-only lifecycle |
| `tests/ApexPost.Tracking.UnitTests/Support/TestData.cs` | Existing fictional test data helpers |

**Prompt text (paste as is):**

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

## Prompt B – BDD feature `ShipmentStatusUpdate.feature` with Reqnroll steps

**Context files to open:**

| File | Why Copilot needs it |
| --- | --- |
| `tests/ApexPost.Specs/Features/ScanSync.feature` | Style example: business language, Background, Scenario Outline |
| `tests/ApexPost.Specs/Steps/ScanSyncSteps.cs` | Style example: Reqnroll bindings with constructor injection |
| `tests/ApexPost.Specs/Contexts/ScanSyncContext.cs` | Style example: per-scenario context class |
| `tests/ApexPost.Specs/Hooks/ScenarioHooks.cs` | How hooks are scoped to a feature |
| `tests/ApexPost.Specs/Support/SpecCourier.cs` | Ready `ICurrentCourier` for the service |
| `tests/ApexPost.Tracking.UnitTests/Support/InMemoryTracking.cs` | In-memory repositories (linked into the Specs project) |
| `src/ApexPost.Application/Tracking/ShipmentTrackingService.cs` | Service under test (`ChangeStatusAsync`, `GetAsync`) |
| `src/ApexPost.Domain/Tracking/ShipmentStatusRules.cs` | Forward-only lifecycle |
| `src/ApexPost.Application/Tracking/InvalidStatusTransitionException.cs` | The refusal for a backward move |

**Prompt text (paste as is):**

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

## Where to save the output

| Prompt | Raw file (unchanged Copilot answer) |
| --- | --- |
| A | `docs/copilot/raw/prompt-a-shipment-tracking-service-tests.md` |
| B | `docs/copilot/raw/prompt-b-shipment-status-update-feature.md` |

Start each raw file with three header lines (tool and version, model, date), then a line `---`, then the pasted
answer. Do not fix, format or shorten the answer: every change is made later in the test project and recorded in
`COPILOT-LOG.md`.
