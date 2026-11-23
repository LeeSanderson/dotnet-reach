# Contributing

## Running the tests

Plain `dotnet test` runs everything, the acceptance tests included. There is no separate fast
suite and no opt-in flag.

```
dotnet test
```

To narrow a run, use an ordinary filter for the area you touched. For example, to run only the
call-graph tests:

```
dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -- --filter-namespace "Reach.Tests.Graph*"
```

The acceptance tests live in the `Reach.Tests.Acceptance` namespace. CI runs them as a separate
step from the rest of the suite, so a failing step names the failing layer.

Running Reach on its own repository is not a fast path. Almost any change to Reach selects the
whole acceptance layer, and that is correct: the acceptance tests run Reach end to end, so they
reach nearly all of its code.
