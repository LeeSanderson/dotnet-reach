using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class Gen
{
    public static async Task<int> Aw() { await Task.Yield(); return 1; }
    public static IEnumerable<int> It() { yield return 1; }
    public static Func<int,int> Lam() => x => x + 1;
    public static int Local() { int F(int x) => x + 1; return F(1); }
    public static readonly Func<int,int> Cached = x => x * 2;
}
