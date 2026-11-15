# Mutation proof evidence

A mutation proof shows that a Copilot-assisted test really protects a rule: break the production code on purpose,
see the test fail, restore the code, see it pass. `COPILOT-LOG.md` needs at least two.

## Files per mutation

| File | Content |
| --- | --- |
| `mutation-NN-<short-name>-failing.txt` | Test output with the production code broken |
| `mutation-NN-<short-name>-passing.txt` | Test output after the code was restored |

Save the console output unchanged, for example:

```bash
dotnet test tests/ApexPost.Tracking.UnitTests -c Release \
  --filter "FullyQualifiedName~ShipmentTrackingServiceCopilotTests" \
  --logger "console;verbosity=normal" > docs/copilot/evidence/mutation-01-scan-age-failing.txt
```

## Rules

- The broken code is **never committed**. Restore it with `git restore <file>` and check with `git status` and
  `git diff` that the production file is unchanged before saving the passing output.
- Each failing file must show the test name and the assertion that failed (not a compile error).
- `COPILOT-LOG.md` records for each mutation: the file and line changed, the exact change (before → after), the test
  that failed, and the names of these two files.
- No machine names or personal paths: if the output contains them, replace only that part with `<path>` and say so
  in the log.
