# Reach

Work out which tests a code change could possibly affect, and run only those.

```bash
dnx dotnet-reach@0.1.0-alpha.1 select --base origin/main
dotnet test --filter "$(…)"   # …or read .reach/report.json and run what it names
```

Reach reads your **compiled assemblies**, walks the call graph **backwards** from the code you
changed to the tests that can reach it, and writes a report naming them.

Four things to know before you decide whether it is for you. It is **static analysis** — it
never runs your tests, and never watches one run. It works from **compiled assemblies and debug
symbols**, not from source, so it sees what the compiler actually produced. It **over-selects on
purpose**: where the implementation that runs is not knowable, every candidate is selected. And
it **never narrows** on unproven evidence, because a test that should have run and did not is a
regression nobody sees.

> ### If Reach exits non-zero, run the whole suite or stop the build.
>
> That is the whole contract. An empty selection exits `0`; non-zero means *do not trust my
> answer*. Wire that one line and Reach cannot cost you a regression.

## Where to go next

| You want to | Read |
|---|---|
| wire Reach into a pipeline | [Adopting Reach](docs/adopting-reach.md) |
| parse `report.json`, or understand a selection you do not believe | [The report schema](docs/report-schema.md) |
| know what Reach cannot do, and which way each gap fails | [The limitations register](docs/limitations.md) |
| know why Reach exists and where it is going — background, not a manual | [PRD.md](PRD.md) |

The command line documents itself: `dnx dotnet-reach@0.1.0-alpha.1 select --help`.

## Status

`0.1.0-alpha.1` — the walking skeleton. It selects, it reports, and it is honest about what it
cannot see. It has not yet been measured against a large real solution; the
[limitations register](docs/limitations.md#the-stop-condition) records the stop condition that
was fixed before any number existed.

## Licence

[MIT](LICENSE).
