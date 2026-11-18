# 01: Standardise on CRLF

**What to build:** The repository stores and checks out text files with CRLF on every platform, with shell scripts kept LF, and git and the formatter agree about it everywhere. This alone should get the Windows leg past `Verify formatting` and let its tests run for the first time in a while. See [the spec](../spec.md) and ADR-0017.

- `.gitattributes`: `text=auto` with `eol=crlf` for everything, plus explicit `eol=lf` exceptions for `*.sh` and everything under `.husky/`. The existing `*.sln` / `*.slnx` lines are folded into the CRLF default.
- `.editorconfig`: `end_of_line = crlf` under `[*]`. Rewrite the comment that says the setting is "left unset on purpose".
- Renormalise the whole repository in a commit of its own with no other changes, so it can be skipped in blame.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `.gitattributes` and `.editorconfig` both state CRLF, with LF exceptions for shell scripts and Husky hooks
- [ ] The renormalisation is a standalone commit
- [ ] `dotnet format --verify-no-changes --no-restore` passes locally on Windows
- [ ] On a Linux checkout, the Husky hook scripts have LF endings and the pre-commit hook runs
- [ ] The Windows CI leg gets past `Verify formatting` and runs `Test`. Fix any Windows-only test failures it reveals here, or, if one is non-trivial, record it under `## Comments` and raise it with the maintainer
- [ ] The Linux CI leg shows no new failures beyond the 7 known ones
