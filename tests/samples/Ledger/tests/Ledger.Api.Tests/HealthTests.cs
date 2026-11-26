using Xunit;

namespace Ledger.Api.Tests;

public class HealthTests
{
    [Fact]
    public void Health_check_reports_ok() => Assert.Equal("ok", HealthEndpoint.Check(storeReachable: true));
}
