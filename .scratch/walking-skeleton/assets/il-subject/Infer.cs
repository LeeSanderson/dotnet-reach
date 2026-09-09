using System;
public interface IShape { double Area(); }
public class Sq : IShape { public double Area() => 1; public override string ToString() => "s"; }

public static class Inf
{
    // unconstrained generic receiver
    public static string Unconstrained<T>(T x) => x.ToString();
    // constrained generic receiver
    public static double Constrained<T>(T x) where T : IShape => x.Area();
    // local variable receiver
    public static string ViaLocal() { var s = new Sq(); Sq t = s; return t.ToString(); }
    // field receiver
    private static Sq _f = new Sq();
    public static string ViaField() => _f.ToString();
    // branch merge: two different static types onto the stack
    public static string Merge(bool b, Sq p, Plain q) => (b ? (object)p : (object)q).ToString();
    // interface-typed receiver (the DI shape)
    public static double ViaIface(IShape s) => s.Area();
}
