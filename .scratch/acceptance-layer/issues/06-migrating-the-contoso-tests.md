# Migrating the Contoso acceptance tests

Type: grilling
Status: open
Blocked by: 04

## Question

Contoso becomes the first small sample solution, and its 13 acceptance tests
(`tests/Reach.Tests/Acceptance/AcceptanceTests.cs`) move onto patches. Not all of them are
selection checks: report determinism, the consumer loop over `invocations`, and the committed
example report (`ExampleReportTests`) assert other things.

For each of the 13 plus `ExampleReportTests`: does it become an acceptance test, stay as C#
over the new harness, or move elsewhere? And does the Contoso fixture itself need to change
to fit the declaration format?
