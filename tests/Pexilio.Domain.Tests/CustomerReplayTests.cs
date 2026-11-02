using Pexilio.Application;

namespace Pexilio.Domain.Tests;

public sealed class CustomerReplayTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void A_replay_after_debt_replaces_only_the_current_outstanding_amount()
    {
        var stored = Stored("0.0000");

        var applied = CustomerReplay.TryWithCurrentOutstanding(stored, 15.2500m, out var response);

        Assert.True(applied);
        Assert.Equal("15.2500", response.OutstandingAmount);
        Assert.Equal(stored with { OutstandingAmount = "15.2500" }, response);
    }

    [Fact]
    public void A_stored_response_without_a_balance_still_uses_the_current_debt()
    {
        var stored = Stored("");

        var applied = CustomerReplay.TryWithCurrentOutstanding(stored, 6.0000m, out var response);

        Assert.True(applied);
        Assert.Equal("6.0000", response.OutstandingAmount);
        Assert.Equal("Sara", response.Name);
        Assert.Equal("0551000001", response.Phone);
    }

    [Fact]
    public void A_negative_current_balance_is_rejected_and_the_stored_identity_is_kept()
    {
        var stored = Stored("0.0000");

        var applied = CustomerReplay.TryWithCurrentOutstanding(stored, -0.0001m, out var response);

        Assert.False(applied);
        Assert.Equal(stored, response);
    }

    private static CustomerResponse Stored(string outstanding) =>
        new(
            CustomerId,
            "individual",
            "Sara",
            "0551000001",
            "Rue 1",
            "note",
            null,
            null,
            null,
            null,
            null,
            true,
            outstanding);
}
