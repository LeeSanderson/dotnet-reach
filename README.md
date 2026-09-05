# dotnet-reach

Large .NET codebases run their entire test suite on every pull request, even though most changes only affect a small part of the system. That wastes CI minutes and slows down feedback.

Reach is a command-line tool that looks at what a developer changed, works out which tests could possibly be affected by that change, and tells the build to run only those. It works out the answer by reading the compiled application — following the chain of "who calls what" backwards from the changed code until it arrives at a test — rather than by watching previous test runs.

The name is the question it answers: *what does this change reach?*

See [PRD.md](PRD.md) for more details.
