using System;

public class Overrider { public override string ToString() => "o"; }
public class Plain { }
public sealed class Res : IDisposable { public void Dispose() { } }

public static class Sites
{
    // receiver static type declares an override
    public static string OnOverrider(Overrider x) => x.ToString();
    // receiver static type does NOT declare an override
    public static string OnPlain(Plain x) => x.ToString();
    // receiver statically object
    public static string OnObject(object x) => x.ToString();
    // interpolation of a concrete struct
    public static string Interp(int n, Overrider o) => $"{n}-{o}";
    // using over a concrete sealed type
    public static void Using() { using var r = new Res(); }
    // using over the interface
    public static void UsingIface(IDisposable d) { using (d) { } }
}
