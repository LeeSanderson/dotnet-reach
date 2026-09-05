# Change-set edge cases: deletions, renames, untracked files

Type: grilling
Status: open
Blocked by: (none)

## Question

The tiered resolution of unmappable changes handles the common cases. Several edges are
undecided, and each one can cause under-selection.

**Deleted methods.** A method removed in the working tree has no declaration to join, so
it produces no graph identity and the reverse walk has nothing to start from. Worse, the
tier that catches it — whole-assembly selection for a changed file with no changed method
— does not fire if *other* methods in the same file did change. What is the rule?

**Deleted files.** No document to look up in the debug symbols, since the symbols describe
the current build. Does a deleted file select the assemblies that previously contained it,
and how is "previously" established without baseline build artifacts?

**Renamed and moved files.** Git may report these as a rename with similarity, or as a
delete plus an add. Does Reach care about the distinction?

**Untracked files.** A new test file never `git add`ed is invisible to every `git diff`
variant, yet PRD §4.2 requires tests that are new since the baseline to be selected. The
charting session leaned toward including them via `git ls-files --others
--exclude-standard`, on the grounds that excluding them under-selects. Confirm, and decide
whether it is default-on or opt-in — it is the kind of default that surprises people.

**Changes outside the analysis scope.** A changed file that belongs to no project in any
test project's closure. Selecting nothing is correct by ADR-0002, but silently selecting
nothing is how a tool loses trust — what does the report say?

**Ignored files.** A change to a `.gitignore`d file that is nonetheless compiled or copied
to output.

Every answer must state which direction it errs in, and why that direction is safe.
