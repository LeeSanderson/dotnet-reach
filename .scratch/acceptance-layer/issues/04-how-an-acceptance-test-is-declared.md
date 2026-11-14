# How an acceptance test is declared

Type: grilling
Status: open
Blocked by: 01

## Question

Given the patch format chosen in [How a patch and its expected result read](01-patch-format-and-readability.md):
is an acceptance test **data** — one folder per test (patch, expected result, a short
README saying what it shows), all discovered and run by a single xUnit theory — or **code**,
one C# test method per acceptance test with its patch alongside?

Settle too: where sample solutions and acceptance tests live in the tree, how an acceptance
test names its sample solution, and the exact shape of the expected result's file.
