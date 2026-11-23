namespace Ledger.Api;

/// <summary>
/// Says whether the service is up. Reaches neither Billing nor Dunning.
/// </summary>
public static class HealthEndpoint
{
    public static string Check(bool storeReachable) => storeReachable ? "ok" : "degraded";
}
