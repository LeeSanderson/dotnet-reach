# IL subject assembly

The evidence behind [Generics, delegates and function
pointers](../../issues/05-generics-delegates-and-function-pointers.md). Every IL claim in
that ticket's answer was read off this assembly rather than recalled, and two working
assumptions were overturned by doing so.

```
dotnet build -c Release
ilspycmd -il -t Sites bin/Release/net8.0/Subject.dll
ilspycmd -il -t Gen   bin/Release/net8.0/Subject.dll
ilspycmd -il -t Inf   bin/Release/net8.0/Subject.dll
```

`Release` matters: `ViaLocal` only collapses to `newobj; callvirt` — the exact-type rung of
the inference ladder — once the optimiser has run.

## What each type demonstrates

- **`Sites`** — that Roslyn emits `callvirt` against the *slot-defining* declaration, not
  the most-derived static type (`OnOverrider`), that a sealed type's `using` still dispatches
  through `IDisposable` (`Using`), and that string interpolation never names `ToString` at
  all (`Interp`).
- **`Gen`** — `[AsyncStateMachine]` and `[IteratorStateMachine]` on the kernel methods; that
  `Aw` reaches its own body only via `AsyncTaskMethodBuilder.Start`; that a lambda is
  `ldftn` in its kernel method (`Lam`) while a `static readonly` one is `ldftn` in `.cctor`
  (`Cached`); and that a local function is an ordinary `call` (`Local`).
- **`Inf`** — the receiver-type inference ladder. Exact type from `newobj` (`ViaLocal`),
  signature type from `ldsfld` (`ViaField`), the generic constraint arriving inside the
  token (`Constrained`), the fall-through for an unconstrained receiver (`Unconstrained`),
  a stack merge with two incompatible types (`Merge`), and the interface-typed receiver that
  inference cannot help with (`ViaIface`).

Kept as a seed for [Fixture catalogue](../../issues/11-fixture-catalogue.md), which owns the
question of what the real fixture solutions must cover. This is a disassembly subject, not a
fixture: it has no tests and proves nothing about selection.
