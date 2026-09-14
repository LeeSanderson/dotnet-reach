# The process port and the git adapter

Status: ready-for-agent
Depends on: 01
Spec: [§16.1](../../walking-skeleton/spec.md#161-projects-ports-and-seams), [§9.1](../../walking-skeleton/spec.md#91-sources)

## Goal

`IProcessRunner` — **the only port in Reach** — plus the git operations every later phase
needs, and a fake good enough to test five CI environment variables, a shallow clone and a
failed build without any of those things being true.

## Scope

**The port.** A small interface hiding process lifetime, cancellation, stream draining and
exit-code handling: take an argument vector and a working directory, return exit code,
stdout and stderr. It is a deep module — the interface is much smaller than what it hides.

It covers **two** adapters, `git` and `dotnet build`, which is what makes it a real seam
rather than a hypothetical one. Do not add a second port for anything else. In particular:

- **No filesystem abstraction.** Real temporary directories are less work than mock
  filesystem setup, every filesystem test here needs a real `git` repository anyway, and the
  abstraction is viral — it changes every signature that touches a path for a fake nothing
  needs.
- **No metadata-reader port.** Ticket 10 takes already-opened `PEReader`s as parameters.

**The git operations**, each an argument vector through the port, never a shell string:

| Purpose | Command |
|---|---|
| resolve a merge-base | `git merge-base HEAD <ref>` |
| resolve a ref to a SHA | `git rev-parse <ref>` |
| detect a shallow clone | `git rev-parse --is-shallow-repository` |
| the change set | `git diff --no-renames --name-status <baseline>` |
| untracked files | `git ls-files --others --exclude-standard` |
| a file at the baseline | `git show <baseline>:<path>` |
| repository root | `git rev-parse --show-toplevel` |

`git diff --no-renames --name-status <baseline>` spans committed-since-baseline, staged **and**
unstaged in one shot. `--no-renames` is load-bearing, not tidiness: it is what makes ticket
06's declared-type matching the ground truth rather than git's similarity threshold, whose
wrong setting under-selects.

**The fake.** A scripted `IProcessRunner` matching on argument vector, so a test can assert
the exact argv issued. Every later ticket's unit tests depend on it, so it is worth getting
right here.

## Acceptance criteria

- A real-process integration test over a temp git repository: `git init`, commit, branch,
  commit again, and assert `merge-base` returns the first commit's SHA.
- Non-zero exit from the child process surfaces as a result, never an exception.
- stdout and stderr are drained concurrently — a child writing more than a pipe buffer to
  stderr while Reach reads stdout must not deadlock. Assert with a deliberately chatty child.
- Cancellation kills the child process tree.
- Paths with spaces and non-ASCII characters survive, asserted on both operating systems.
- The fake can script a failure, an empty output and a multi-line output, and records the argv
  it was asked for.

## Out of scope

The `dotnet build` adapter is ticket 07 — it uses this port but adds the layout-switch refusal
and the exit-2 behaviour. Baseline *resolution* is ticket 03; this ticket supplies only the
commands it calls.
