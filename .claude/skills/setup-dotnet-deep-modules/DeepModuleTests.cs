// Deep-module rules. Copy into the architecture test project and adapt the namespace,
// the `Modules` list, and `HostAssemblyName`. See src/README.md for the convention these
// enforce, and the setup-dotnet-deep-modules skill for why only two of the four rules
// need a test at all: the compiler enforces the other two.

using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Reach.ArchitectureTests;

public class DeepModuleTests
{
    /// <summary>One marker type per module project. Add a line when you add a project.</summary>
    private static readonly Assembly[] Modules =
    [
        typeof(Reach.Example.Greeter).Assembly,
    ];

    /// <summary>The entry-point project. Dependencies point inward, so nothing may depend on it.</summary>
    private const string HostAssemblyName = "Reach.Cli";

    /// <summary>Folder (and so namespace segment) that holds a module's implementation.</summary>
    private const string ImplementationSegment = "Internal";

    // Rule 1a — implementation stays internal.
    // The compiler already stops another project reaching an `internal` type. What it has no
    // opinion on is a type under Internal/ being marked `public` by accident, which silently
    // widens the interface. That is what this catches.
    [Fact]
    public void Implementation_types_are_not_public()
    {
        var leaked = Modules
            .SelectMany(module => module.GetExportedTypes())
            .Where(type => type.Namespace?.Contains($".{ImplementationSegment}", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        leaked.Should().BeEmpty(
            "types under {0}/ are implementation and must be internal",
            ImplementationSegment);
    }

    // Rule 1b — the public surface is flat.
    // Public types live in the project's root namespace, so a module's interface is a short
    // list you can read in one screen rather than a tree you have to go spelunking through.
    [Fact]
    public void Public_types_live_in_the_root_namespace()
    {
        var buried = Modules
            .SelectMany(module => module.GetExportedTypes()
                .Where(type => !type.IsNested)
                .Where(type => type.Namespace != module.GetName().Name)
                .Select(type => type.FullName))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        buried.Should().BeEmpty(
            "a module's public surface is its root namespace; move these under {0}/ and make them internal",
            ImplementationSegment);
    }

    // Rule 3 — tests go through the public surface, not around it.
    // InternalsVisibleTo dissolves the whole scheme, so it is banned outright rather than
    // allowed "just for tests". A test that needs internals is testing past the interface.
    [Fact]
    public void Modules_do_not_grant_InternalsVisibleTo()
    {
        var grants = Modules
            .SelectMany(module => module
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => $"{module.GetName().Name} -> {attribute.AssemblyName}"))
            .OrderBy(grant => grant, StringComparer.Ordinal)
            .ToArray();

        grants.Should().BeEmpty(
            "a test that needs internals is testing past the interface");
    }

    // Rule 4 — dependencies point inward.
    // ProjectReference cycles are already a build error, so what is left is direction: the host
    // wires modules together, and no module may depend on the host.
    [Fact]
    public void No_module_depends_on_the_host()
    {
        foreach (var module in Modules)
        {
            var result = Types.InAssembly(module)
                .Should().NotHaveDependencyOn(HostAssemblyName)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "{0} must not depend on the host {1}; inspect result.FailingTypes for the offenders",
                module.GetName().Name,
                HostAssemblyName);
        }
    }

    // Add one test per layering rule this repo actually has. Delete this comment if it has none;
    // a rule invented to fill space is worse than no rule.
    //
    // [Fact]
    // public void Domain_does_not_depend_on_infrastructure()
    // {
    //     var result = Types.InAssembly(typeof(Reach.Domain.Marker).Assembly)
    //         .Should().NotHaveDependencyOn("Reach.Infrastructure")
    //         .GetResult();
    //
    //     result.IsSuccessful.Should().BeTrue("the domain must not know how it is persisted");
    // }
}
