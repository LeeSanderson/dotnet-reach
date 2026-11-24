// Every test may run in parallel with every other, methods of one class included: an acceptance
// test spends most of its time in a sample build, and one class holds a sample's rows. A test that
// cannot share the process opts out with DisableParallelization, on the method or the class.
// MaxThreads is left at its default (one per processor). Sample builds are throttled separately,
// machine-wide, by Fixtures/BuildSlots.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.All)]
