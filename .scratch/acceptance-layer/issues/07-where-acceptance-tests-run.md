# Where the acceptance tests run

Type: grilling
Status: open
Blocked by: 02

## Question

Do acceptance tests stay in `Reach.Tests` (filtered by namespace, as today) or move to their
own test project? How does a developer run the fast suite without them, and how does CI run
them — every push, on both Windows and Linux, as a separate job? Is there a time budget the
layer must stay inside as it grows, and what happens when it is exceeded?

Decided on the build-cost facts from
[What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md).
