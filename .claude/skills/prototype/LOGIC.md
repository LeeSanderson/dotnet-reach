# Logic Prototype

A **driveable demo** that lets someone push a state model through cases by hand. Use this when the question is about **business logic, state transitions, or data shape**: the kind of thing that looks reasonable on paper but only feels wrong once you push it through real cases.

## When this is the right shape

- "I'm not sure if this state machine handles the edge case where X then Y."
- "Does this data model actually let me represent the case where..."
- "I want to feel out what the API should look like before writing it."
- Anything where someone wants to **press buttons and watch state change**.

If the question is "what should this look like," this is the wrong branch. Use [UI.md](UI.md).

## Pick the artifact: who has to drive it?

The audience decides, and it decides what survives:

- **A C# harness** (default). A scratch console project driven by `dotnet run`, or a `dotnet watch`ed Blazor page. The model is a real C# type in the real language, so once the question is answered it lifts into production **verbatim**. Choose this whenever the people judging the model can run `dotnet run`.
- **A single self-contained HTML file**. One file, nothing to install, opens by double-click and survives being emailed around. Choose this only when a non-developer (a designer, a PM, a domain expert) has to drive it themselves. The cost is real: the model is transcribed into JavaScript, so what lifts back into C# afterwards is the **validated model** (the states, transitions and invariants you learned), not the code. Budget for writing the C# twice.

Both shapes follow the same process below. Where it matters, the HTML-only detail is called out.

## Process

### 1. State the question

Before writing code, write down what state model and what question you're prototyping. One paragraph, at the top of the demo (in a visible intro, not just a comment). A logic prototype that answers the wrong question is pure waste, so make the question explicit so it can be checked later, whether the user is watching now or returning to it AFK.

### 2. Isolate the logic in a portable module

Put the actual logic (the bit that's answering the question) in one small, pure module that could be lifted out and dropped into the real codebase later. In the C# harness that's a single file with no `Console` in it; in the HTML file it's a single `<script>` block. The shell around it is throwaway; this module isn't.

The right shape depends on the question:

- **A pure reducer**: `static State Reduce(State state, Command command)` over `record` types. Good when actions are discrete events and state is a single value.
- **A state machine**: explicit states and transitions, states as a closed set (an `enum`, or a sealed record hierarchy). Good when "which commands are even legal right now" is part of the question.
- **A small set of pure functions** over a plain `record`. Good when there's no implicit current state, just transformations.
- **A class with a clear method surface** when the logic genuinely owns ongoing internal state.

Prefer immutable `record`s and `with` expressions: a reducer that returns a new state instead of mutating one makes "what changed" trivial to render, and the walkthroughs replayable.

Pick whichever shape best fits the question being asked, *not* whichever is easiest to wire to a shell. Keep it pure: no `Console`, no DOM, no `document`, no button handlers reaching inside it. The shell calls into it; nothing flows the other direction. This is what makes the prototype useful past its own lifetime: in the C# harness the validated reducer / machine / function set lifts into the real project as-is.

### 3. Build the shell around it

**C# harness**: a scratch console project. Print the state, list the available commands, read a keystroke, dispatch, re-render. [Spectre.Console](https://spectreconsole.net) is worth the reference for a readable state panel and a `SelectionPrompt` of commands, but `Console.WriteLine` and a `switch` is enough. Keep it to one `Program.cs` beside the pure module. A `dotnet watch`ed Blazor page is the same thing with buttons, if the state is easier to read as a table.

**HTML file**: one file, plain HTML/CSS/JS: no framework, no bundler, no server, everything inline so it opens by double-click and survives being emailed around. Anyone should be able to run it by opening it.

Either way, write it for the person driving it. Every label is in **domain language**, not code: commands and state read like the business, not the reducer. Explain in plain words what's happening.

Lay it out with a clean hierarchy, top to bottom:

1. **Title and one-line explanation** of what this demo lets you explore (the question from step 1).
2. **Current state**: the full relevant state, rendered as a readable panel (labelled fields, not a raw JSON dump or a bare `ToString()`), re-rendered after every step so the change is visible. Where it helps the driver follow, call out what just changed.
3. **Free play**: one command per action, always offered, so anyone can poke at the model in any order. Each one dispatches its action and re-renders the state.
4. **Guided walkthroughs**: a set of **scenarios**. Each holds a short plain-language description (the situation it sets up and what to watch for) and underneath it the ordered **steps** for that scenario, each step a real command that performs the action and advances. Starting a walkthrough resets to a known initial state so the scenario runs the same way every time. In HTML that's one tab per scenario; in the console harness it's a numbered menu.

Choose scenarios that demonstrate the awkward cases, the ones hard to reason about on paper: the happy path, a tricky edge case, an attempt at something that should be illegal.

Keep it restrained: clear labels, generous spacing, one accent colour. No animations, no gimmicks: nothing that competes with the state and the commands.

### 4. Hand it over

Give them the command to run, or send them the file and open it for them. They'll work through the walkthroughs and free-play whenever they get to it; the interesting moments are when they say "wait, that shouldn't be possible" or "huh, I assumed X would be different"; those are the bugs in the _idea_, which is the whole point. If they want new actions or a new scenario, add them. Prototypes evolve.

### 5. Capture the answer and the prototype

Once the prototype has answered its question, capture the answer, then capture the prototype the way the [SKILL](SKILL.md) describes. The logic-specific mapping: the validated reducer / machine / function set lifts into the real project (the decision, absorbed) and the shell rides along to the throwaway branch that keeps the prototype as a primary source. Keep the shell re-runnable there: a scratch console project needs its `.csproj` and, if the solution filters projects, a note on how to run it outside the solution; an HTML file is trivially re-runnable already.

## Anti-patterns

- **Don't add tests.** A prototype that needs tests is no longer a prototype.
- **Don't wire it to the real database.** Use in-memory state unless the question is specifically about persistence.
- **Don't generalise.** No "what if we wanted to support X later." The prototype answers one question.
- **Don't blur the logic and the shell together.** If the pure module references `Console`, the DOM, `document`, or input handlers, it's no longer liftable. Keep the shell thin over a pure module.
- **Don't add it to the solution's real projects.** A scratch console project referenced by the app, or logic slipped into a production project "just for now", stops being throwaway. Keep it standalone.
- **Don't reach for a framework, bundler, or server in the HTML shape.** One file the recipient double-clicks; a Blazor WASM app or a dev server defeats "shareable" — if you need a framework, you wanted the C# harness.
- **Don't ship the shell into production.** It's optimised for being driven by hand. The logic module behind it is the bit worth keeping.
