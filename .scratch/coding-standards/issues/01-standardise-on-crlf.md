# 01: Standardise on CRLF

**What to build:** The repository stores and checks out text files with CRLF on every platform, with shell scripts kept LF, and git and the formatter agree about it everywhere. This alone should get the Windows leg past `Verify formatting` and let its tests run for the first time in a while. See [the spec](../spec.md) and ADR-0017.

- `.gitattributes`: `text=auto` with `eol=crlf` for everything, plus explicit `eol=lf` exceptions for `*.sh` and everything under `.husky/`. The existing `*.sln` / `*.slnx` lines are folded into the CRLF default.
- `.editorconfig`: `end_of_line = crlf` under `[*]`. Rewrite the comment that says the setting is "left unset on purpose".
- Renormalise the whole repository in a commit of its own with no other changes, so it can be skipped in blame.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] `.gitattributes` and `.editorconfig` both state CRLF, with LF exceptions for shell scripts and Husky hooks
- [x] The renormalisation is a standalone commit (no-op: see Comments)
- [x] `dotnet format --verify-no-changes --no-restore` passes locally on Windows
- [x] On a Linux checkout, the Husky hook scripts have LF endings and the pre-commit hook runs
- [ ] The Windows CI leg gets past `Verify formatting` and runs `Test`. Fix any Windows-only test failures it reveals here, or, if one is non-trivial, record it under `## Comments` and raise it with the maintainer
- [ ] The Linux CI leg shows no new failures beyond the 7 known ones

## Comments

- **Renormalisation was a no-op, so there is no renormalisation commit.** `git add --renormalize .`
  staged nothing: every tracked file was already stored LF in the index (`git ls-files --eol`
  showed 385 × `i/lf`). Git always stores `text` files as LF; `eol=crlf` only changes what is
  written to the working tree on checkout. So there is nothing for blame to skip. Existing
  clones pick up CRLF only when files are rewritten — e.g. `git rm -r -q --cached . && git
  reset --hard` on a clean tree.
- `.editorconfig` carries the LF exception too (`[{*.sh,.husky/**}] end_of_line = lf`), so the
  formatter and git agree for the shell scripts as well.
- Linux check (fresh clone in the `mcr.microsoft.com/dotnet/sdk:10.0` container, autocrlf unset):
  `.husky/*` and `*.sh` check out `w/lf`, other files `w/crlf`; `dotnet format
  --verify-no-changes` passes; a commit staging a `.cs` change ran the Husky `format-staged`
  task successfully.
- **Windows-only test failure fixed:** `ExampleReportTests` failed on the CRLF checkout because
  the raw string literal in `TestRunnerConfiguration` took its line endings from the source
  file, putting `\r\n` into the `test-runner-not-configured` notice. Fixed with
  `ReplaceLineEndings("\n")`. `CheckoutGuidance` (the exit-4 message) had the same latent issue,
  which no test caught, and got the same fix. Both now produce the LF output they had before
  this ticket, on every platform. Local Windows `dotnet test`: 521/521 pass.
