# The repository standardises on CRLF, except shell scripts

Text files are stored and checked out with CRLF line endings on every platform
(`* text=auto eol=crlf` in `.gitattributes`, `end_of_line = crlf` in `.editorconfig`). Shell
scripts — `*.sh` and everything under `.husky/` — are the exception and stay LF, because bash
on Linux and macOS fails on a carriage return.

This reverses an earlier choice of LF everywhere. LF was picked so that
`dotnet format --verify-no-changes` agreed with itself on both CI legs, but `.editorconfig`
left `end_of_line` unset, so on Windows the formatter fell back to `Environment.NewLine` and
demanded CRLF on any line it re-wrapped — failing the Windows leg. Adopting
Opinionated.DotNet.CodingStandards, whose global config sets `end_of_line = crlf`, made the
mismatch fail on both legs. Overriding the package back to LF was a one-line alternative; CRLF
was chosen instead so the repository matches the coding standard rather than carrying a
standing exception to it, accepting a one-off renormalisation of every file.

## Consequences

`.gitattributes` and `.editorconfig` must state the same line ending; either one alone lets
`dotnet format` and git disagree on some platform. Do not "fix" this back to LF without
reading the above.
