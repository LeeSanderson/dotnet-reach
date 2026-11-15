# The small sample solutions

Type: grilling
Status: open
Blocked by: (none)

## Question

[Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)
puts rows 1–16 (gate) and 24–25 (known gaps) on **small sample solutions**, and fixes one of
them: a "frameworks" sample with a test project per dialect (`xunit-v2`, `xunit-v3`,
`xunit-v3-4`, `nunit`, `mstest`, unrecognised) over one production project.

How do the rest group into samples? How many, what each holds, and which rows each carries.
Does **Contoso become the frameworks sample**, or a different one, or retire?

Constraints carried from that ticket:
- An expected section that is given is exact, so a sample must not trip notices it does not
  mean to. Row 11 (the runner-setting notice) and a clean dialect row may not share a sample
  without listing the notice everywhere.
- Shapes no fixture has yet: interface/virtual dispatch, a multi-targeted *test* project, a
  platform-suffixed framework.
- Each sample costs one build per acceptance test (4–6 s, from
  [What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md)).
