# Raw Copilot output

This folder holds the answers of GitHub Copilot Chat **exactly as Copilot gave them**. They are evidence, not code:
the files are Markdown, they are not compiled, and they are never edited after they are saved.

## Rules

- One file per prompt, named as in `docs/copilot/PROMPTS.md`:
  - `prompt-a-shipment-tracking-service-tests.md`
  - `prompt-b-shipment-status-update-feature.md`
- First lines of each file:

  ```text
  Tool: GitHub Copilot Chat in <VS Code x.y / Visual Studio 2026>, Ask mode
  Model: <model shown in the Copilot model picker>
  Date: <yyyy-mm-dd>
  ---
  ```

  Then the complete answer, pasted with the chat's copy button.
- If Copilot was asked to `continue`, append the second answer below a line `--- continue ---`.
- If a prompt is run again (for example after a wrong answer), save it as a new file with `-run2` at the end of the
  name and keep the first one. `COPILOT-LOG.md` says which run was used.
- Do not fix typos, formatting or code here. Every change happens in the test project and is listed in the
  kept / changed / rejected table of `COPILOT-LOG.md`.
- No secrets and no real data: if Copilot invents anything that looks real (an email, a URL, a key), leave the raw
  file as it is and reject that part in the log.
