# Which suite proves each claim

Type: task
Status: open
Blocked by: (none)

## Question

The release gate is **every claim the alpha's docs make, proven by some suite**
([Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)).
The acceptance layer takes rows 1–23 of that ticket; everything else — exit codes, argument
parsing, `--no-build` and exit 5, `--runsettings`, `--paths`, CI baseline detection, shallow
clones, response-file and chunked delivery, `.reach/.gitignore`, the report-shape claims in
`docs/report-schema.md` — is assumed to be covered by an existing test.

AFK: walk every claim in `README.md`, `docs/adopting-reach.md` and `docs/report-schema.md`,
and name the test (file and method) that proves each one outside the acceptance layer. The
output is the claim-to-suite table the spec will carry, plus the list of claims **no test
proves**.

That list is the input to a decision, not the decision itself: for each unproven claim, the
owner chooses a test or cutting the claim from the docs.
