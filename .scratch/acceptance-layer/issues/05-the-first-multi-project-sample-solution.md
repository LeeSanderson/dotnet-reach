# The first multi-project sample solution

Type: grilling
Status: open
Blocked by: 03

## Question

Design the first sample solution shaped like a real codebase: its layers and projects, its
test projects and which framework each uses, and — the reason it exists — which test projects
each planned patch must **not** select.

Driven by the capabilities [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)
assigns to multi-project sample solutions. Bounded by the build cost
[What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md)
measures, if that has resolved.

## Comments

- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):** this sample carries gate rows **17–23** (cross-assembly edge, `const` and
  removed-member recompilation widening, `.csproj` and `Directory.Build.props` containment,
  namespace move, unmapped root file) and known-gap rows **26–27** (interface-typed receiver
  fan-out, a core `const`'s blast radius). Every gate row is about what is *not* selected. Exact
  notice sections mean the sample must not trip notices it does not intend.
