# Change detection is keyed on declared type, not file path

Reach computes the changed set by matching **declared types** between the baseline and the
working tree, keyed on fully-qualified name plus arity, and compares their members. Paths
are read from git and used for the path-based tiers, but git is never asked to interpret
them: `--no-renames` is passed, so a move arrives as a delete plus an add and the type is
simply found on both sides. A deleted file is likewise routed as the deletion of every type
it declared at baseline, rather than through a rule about files.

## Considered options

**Git rename detection.** Pairing a delete with an add by similarity would buy the same
precision on a move-and-edit commit, but it buys it with a tunable threshold, and the
direction a wrong threshold fails in is under-selection. Matching on declared type asks the
*binaries* what still exists instead of asking git what looks similar, so it is ground truth
rather than a heuristic, and it never needs to pair a delete with a specific add.

**Attributing every deleted path by directory containment**, mapping it to its nearest
ancestor project and widening that assembly. Kept, but only as the fallback for a type that
is genuinely gone. As the primary rule it makes a folder reorganisation widen a whole
assembly — a result a reviewer reads as the tool being broken, which is how a tool gets
switched off (PRD §4.3).

**Simple name plus arity** as the matching key, which would keep a namespace move at
whole-type widening. Rejected: a namespace is part of the metadata name, so the move changes
what runtime binding sees, and serialization discriminators, convention-based registration
and `Type.GetType` are blind spots Reach cannot see into.

## Consequences

A folder reorganisation that also syncs namespaces widens every assembly it touches, while a
pure folder move stays at whole-type widening. The asymmetry is defensible — one changes
metadata, the other does not — but it is surprising enough to document rather than leave in
the spec.

The matching is name-keyed, which appears to contradict PRD §9.4's "method identity must be
integers derived from metadata tokens, never strings". It does not: this is the source side,
over the changed files only, and it happens before the **join**. The graph side stays
integer-keyed.

Change detection must read deleted and moved files at the baseline revision
(`git show <baseline>:<path>`), which the decision to parse changed files at both revisions
already requires.

What is compared *within* a matched type is not settled here — a `const`, field initializer
or attribute argument is not a method body, and a compile-time constant is inlined into
consumers whose source never changed. That is
[What counts as a changed member](../../.scratch/walking-skeleton/issues/18-what-counts-as-a-changed-member.md).
