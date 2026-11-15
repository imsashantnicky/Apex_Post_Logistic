# Copilot log – ApexPost Logistics (Week 5)

GitHub Copilot was used by the **user** to draft tests. Claude Code prepared the prompts and helped review the output,
but did not write or edit Copilot's answers. Every generated test was reviewed by a human before it was committed.

- Prompts: `docs/copilot/PROMPTS.md`
- Raw, unedited answers: `docs/copilot/raw/`
- Mutation proof output: `docs/copilot/evidence/`
- Every Copilot-assisted class starts with:
  `// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).`

Decision values: **Kept** (used as generated), **Changed** (used after an edit; the edit is described),
**Rejected** (not used; the reason is given).

## Prompt A – unit tests for `ShipmentTrackingService`

| Item | Value |
| --- | --- |
| Tool and model | _to fill: Copilot Chat version, model_ |
| Date | _to fill_ |
| Context files | As listed in `docs/copilot/PROMPTS.md`, prompt A |
| Raw answer | `docs/copilot/raw/prompt-a-shipment-tracking-service-tests.md` |
| Final test file | `tests/ApexPost.Tracking.UnitTests/Services/ShipmentTrackingServiceCopilotTests.cs` |

### Prompt text

The exact text of prompt A in `docs/copilot/PROMPTS.md` (copied here verbatim once it has been run):

```text
_to fill: paste prompt A exactly as sent_
```

### What Copilot produced

_to fill: number of tests and theories, which cases from the prompt it covered, what it left out, and anything it
invented (APIs that do not exist, wrong messages, real-looking data)._

### Decisions

| # | Generated test | Decision | Reason / edit made |
| --- | --- | --- | --- |
| A1 | _to fill_ | Kept / Changed / Rejected | _to fill_ |

### Check against the Week 5 test rules

| Rule (CLAUDE.md W5.3) | Met by the generated code? | Fix made |
| --- | --- | --- |
| AAA comments | _to fill_ | _to fill_ |
| `Method_Scenario_ExpectedResult` names | _to fill_ | _to fill_ |
| `[Trait("Category", "Unit")]` | _to fill_ | _to fill_ |
| `FakeTimeProvider`, no `DateTime.UtcNow` | _to fill_ | _to fill_ |
| Exact exception type and message | _to fill_ | _to fill_ |
| Fictional data only | _to fill_ | _to fill_ |
| Overlap with `ShipmentTrackingServiceMoqTests` | _to fill_ | _to fill_ |

## Prompt B – `ShipmentStatusUpdate.feature` with Reqnroll steps

| Item | Value |
| --- | --- |
| Tool and model | _to fill: Copilot Chat version, model_ |
| Date | _to fill_ |
| Context files | As listed in `docs/copilot/PROMPTS.md`, prompt B |
| Raw answer | `docs/copilot/raw/prompt-b-shipment-status-update-feature.md` |
| Final files | `tests/ApexPost.Specs/Features/ShipmentStatusUpdate.feature`, `tests/ApexPost.Specs/Contexts/ShipmentStatusContext.cs`, `tests/ApexPost.Specs/Steps/ShipmentStatusUpdateSteps.cs` |

### Prompt text

```text
_to fill: paste prompt B exactly as sent_
```

### What Copilot produced

_to fill: scenarios and steps generated, business language or not, Background and Scenario Outline present,
ambiguous or duplicate step bindings, static state._

### Decisions

| # | Generated scenario / binding | Decision | Reason / edit made |
| --- | --- | --- | --- |
| B1 | _to fill_ | Kept / Changed / Rejected | _to fill_ |

### Check against the Week 5 BDD rules

| Rule (CLAUDE.md W5.6) | Met by the generated code? | Fix made |
| --- | --- | --- |
| Business language only | _to fill_ | _to fill_ |
| Background and Scenario Outline with Examples | _to fill_ | _to fill_ |
| Context injection, no static fields | _to fill_ | _to fill_ |
| `@Spec` tag (`Category=Spec`) | _to fill_ | _to fill_ |
| Service level stated (real service, in-memory ports) | _to fill_ | _to fill_ |
| No step text clashes with existing bindings | _to fill_ | _to fill_ |

## Mutation proofs

Each proof breaks production code on purpose, runs the Copilot-assisted test, restores the code and runs it again.
The broken code is never committed.

| # | Production file:line | Change (before → after) | Test that failed | Failing output | Passing output |
| --- | --- | --- | --- | --- | --- |
| M1 | _to fill_ | _to fill_ | _to fill_ | `docs/copilot/evidence/mutation-01-…-failing.txt` | `docs/copilot/evidence/mutation-01-…-passing.txt` |
| M2 | _to fill_ | _to fill_ | _to fill_ | `docs/copilot/evidence/mutation-02-…-failing.txt` | `docs/copilot/evidence/mutation-02-…-passing.txt` |

Suggested mutations (one per prompt):

- M1: in `ScanAgePolicy.RejectionFor`, change `>` to `>=`, so a scan exactly 72 hours old is rejected
  (the 72-hour case of prompt A must fail).
- M2: in `ShipmentStatusRules.CanMove`, change `Stage(to) > Stage(from)` to `Stage(to) != Stage(from)`, so a backward
  move is allowed (the backward-move scenario of prompt B must fail).

## Summary

| | Prompt A | Prompt B |
| --- | --- | --- |
| Generated tests / scenarios | _to fill_ | _to fill_ |
| Kept | _to fill_ | _to fill_ |
| Changed | _to fill_ | _to fill_ |
| Rejected | _to fill_ | _to fill_ |

## What Copilot did well and where it needed a human

_to fill after the review (a few sentences)._
