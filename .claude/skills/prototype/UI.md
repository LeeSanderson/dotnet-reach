# UI Prototype

Generate **several radically different variations** of one surface, switchable at runtime. The user flips between variants, picks one (or steals bits from each), then throws the rest away.

"Surface" means whatever the user actually looks at: a Blazor page or component, a Razor Page or MVC view, or — for a console tool — a command's rendered output. The switcher differs (a `?variant=` query param on the web, a `--variant` option on the CLI); everything else in this document is the same for both.

If the question is about logic/state rather than what something looks like, this is the wrong branch. Use [LOGIC.md](LOGIC.md).

## When this is the right shape

- "What should this page look like?"
- "I want to see a few options for this dashboard before committing."
- "Try a different layout for the settings screen."
- "How should `reach explain` present the call chain it found?"
- Any time the user would otherwise spend a day picking between three vague mockups in their head.

## Two sub-shapes: strongly prefer sub-shape A

A UI prototype is much easier to judge when it's **butting up against the rest of the app**: real header, real sidebar, real data, real density. A throwaway surface on its own is a vacuum: every variant looks fine in isolation. Default to sub-shape A whenever there's a plausible existing surface to host the variants. Only reach for sub-shape B if the prototype genuinely has no nearby home.

### Sub-shape A: adjustment to an existing surface (preferred)

The page or command already exists. Variants render **on the same surface**, gated by the `variant` param. The existing data fetching, parameters, and auth all stay. Only the rendering swaps. This is the default; pick it unless there's a specific reason not to.

If the prototype is for something that doesn't yet have a surface but *would naturally live inside one* (a new section of the dashboard, a new card on the settings screen, extra output on an existing command), it's still sub-shape A. Mount the variants inside the host.

### Sub-shape B: a new surface (last resort)

Only use this when the thing being prototyped genuinely has no existing surface to live inside (an entirely new top-level page, or a new command).

Create a **throwaway route or command** following whatever convention the project already uses. Don't invent a new top-level structure. Name it so it's obviously a prototype (include the word `prototype` in the route, filename, or command name). Same `variant` pattern.

Before committing to sub-shape B, sanity-check: is there really no existing surface this could be embedded in? An empty surface hides design problems that a populated one would expose.

In both sub-shapes the switcher is identical.

## Process

### 1. State the question and pick N

Default to **3 variants**. More than 5 stops being radically different and starts being noise, so cap there.

Write down the plan in one line, in the prototype's location or a top-of-file comment:

> "Three variants of the settings page, switchable via `?variant=`, on the existing `/settings` route."

This works whether the user is here to push back or not.

### 2. Generate radically different variants

Draft each variant. Hold each one to:

- The surface's purpose and the data it has access to.
- The project's component library / styling system (MudBlazor, FluentUI Blazor, Razor Pages + Bootstrap, Tailwind, plain CSS; Spectre.Console for a terminal, or raw `Console` if that's all the project uses).
- A clear type name per variant, e.g. `VariantA`, `VariantB`, `VariantC`, each in its own file.

Variants must be **structurally different**: different layout, different information hierarchy, different primary affordance, not just different colours. Three slightly-tweaked card grids isn't a UI prototype, it's wallpaper. A terminal equivalent: a table, a tree, and a one-line-per-result summary are three variants; three tables with different column orders are not. If two drafts come out too similar, redo one with explicit "do not use a table" guidance.

### 3. Wire them together

Web (Blazor):

```razor
@* pseudo-code, adapt to the project's framework *@
@page "/settings"

@if (Variant is "B") { <VariantB Data="@data" /> }
else if (Variant is "C") { <VariantC Data="@data" /> }
else { <VariantA Data="@data" /> }

<PrototypeSwitcher Variants="@(new[] { "A", "B", "C" })" Current="@Variant" />

@code {
    [SupplyParameterFromQuery(Name = "variant")]
    private string? VariantParam { get; set; }

    private string Variant => VariantParam ?? "A";
}
```

Terminal:

```csharp
// pseudo-code, adapt to the project's CLI framework
var variant = parseResult.GetValue(variantOption) ?? "A";

IRenderable view = variant switch
{
    "B" => VariantB.Render(report),
    "C" => VariantC.Render(report),
    _ => VariantA.Render(report),
};

AnsiConsole.Write(view);
PrototypeSwitcher.WriteHint(current: variant, variants: ["A", "B", "C"]);
```

For sub-shape A: keep all the existing data fetching above the switch; only the rendered subtree changes per variant.

For sub-shape B: the throwaway route or command mounts the same switch.

### 4. Build the switcher

**On the web**, a small fixed-position bar at the bottom-centre of the screen with three pieces:

- **Left arrow**: cycles to the previous variant (wraps around).
- **Variant label**: shows the current variant key and, if the variant carries a name, that name too, e.g. `B (Sidebar layout)`.
- **Right arrow**: cycles forward (wraps around).

Behaviour:

- Clicking an arrow updates the query param via `NavigationManager.NavigateTo(url, replace: true)` so the variant is shareable and reload-stable.
- Keyboard: `←` and `→` also cycle. Handle `@onkeydown` on the switcher's container (give it `tabindex="0"`), or a small JS interop listener if you want it to work without focus. Don't intercept arrow keys when an `<input>`, `<textarea>`, or `[contenteditable]` is focused.
- Visually distinct from the page (high-contrast pill, subtle shadow) so it's obviously not part of the design being evaluated.
- Hidden outside development: gate on `IWebHostEnvironment.IsDevelopment()` (or `IWebAssemblyHostEnvironment` in Blazor WASM), so a stray prototype merge can't ship the bar to users.

**In a terminal** there's no persistent bar, so the switcher is a dimmed footer line after the output: the current variant plus the exact command to see the next one (`rerun with --variant B`). Hide the option itself from `--help` and gate it on `#if DEBUG` or an environment check, for the same reason.

Put the switcher in a single shared component or helper so both sub-shapes reuse it. Locate it wherever shared UI lives in the project.

### 5. Hand it over

Surface the URL or the command line, and the variant keys. The user will flip through whenever they get to it. The interesting feedback is usually **"I want the header from B with the sidebar from C"**, which is the actual design they want.

### 6. Capture the answer and clean up

Once a variant has won, capture the answer (which variant and why), then capture the prototype the way the [SKILL](SKILL.md) describes. Fold the winner into the real code and move the rest onto the throwaway branch, not into main:

- **Sub-shape A**: fold the winner into the existing surface; drop the losing variants and the switcher from main.
- **Sub-shape B**: promote the winning variant to a real route or command; drop the throwaway one and the switcher from main.

The full set of variants is the primary source, so it lands on the throwaway branch, not the bin, since variant types and the switcher left in the main branch rot fast and confuse the next reader.

## Anti-patterns

- **Variants that differ only in colour or copy.** That's a tweak, not a prototype. Real variants disagree about structure.
- **Sharing too much code between variants.** A shared `<Header>` is fine; a shared `<Layout>` defeats the point. Each variant should be free to throw out the layout.
- **Wiring variants to real mutations.** Read-only prototypes are fine. If a variant needs to write, point it at a stub: the question is "what should this look like", not "does the backend work".
- **Leaving the variant option in the shipped CLI surface.** An undocumented `--variant` that survives into a release is a support question waiting to happen.
- **Promoting the prototype directly to production.** The variant code was written under prototype constraints (no tests, minimal error handling). Rewrite it properly when you fold it in.
