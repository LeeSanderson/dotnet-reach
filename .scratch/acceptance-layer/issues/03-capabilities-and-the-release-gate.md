# Which capabilities the layer demonstrates, and which gate the release

Type: grilling
Status: open
Blocked by: (none)

## Question

The layer has three jobs — release gate, regression net, showcase. That needs a list:
**which of Reach's capabilities must have an acceptance test**, and **which subset must pass
before `0.1.0-alpha.1` is tagged**?

Candidates come from the spec and the register, not from invention: compiled edges across
assembly boundaries, widening (interface and virtual dispatch), whole-type and whole-assembly
widening, recompilation widening (`const`, removed overloads), project-file changes and
`Directory.Build.props` containment, test recognition per framework, whole-project fallback,
the empty selection, the NUnit `~` over-match, multi-targeting.

For each: is it shown in a **small sample solution** or a **multi-project one**, and is it in
the **release-gate batch** or a later one? The answer seeds the design of the first
multi-project sample solution.
