namespace Contoso.Tests.Unrecognised;

/// <summary>
/// A test framework Reach has never heard of, declared in this project so it needs no package.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContosoCheckAttribute : Attribute;
