using Pexilio.Domain.Customers;

namespace Pexilio.Domain.Tests;

public sealed class CustomerStatementTests
{
    private static readonly DateTimeOffset November = new(2026, 11, 15, 22, 30, 0, TimeSpan.Zero);

    [Fact]
    public void An_empty_request_uses_the_company_local_month()
    {
        var resolved = CustomerStatementWindowRules.TryResolve(null, " ", "Africa/Algiers", November, out var window, out var error);

        Assert.True(resolved);
        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 11, 1), window.From);
        Assert.Equal(new DateOnly(2026, 12, 1), window.To);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), window.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 30, 23, 0, 0, TimeSpan.Zero), window.ToUtc);
    }

    [Fact]
    public void A_late_algiers_evening_belongs_to_the_next_civil_day()
    {
        var utc = new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero);

        var resolved = CustomerStatementWindowRules.TryResolve(null, null, "Africa/Algiers", utc, out var window, out _);

        Assert.True(resolved);
        Assert.Equal(new DateOnly(2026, 12, 1), window.From);
        Assert.Equal(new DateOnly(2027, 1, 1), window.To);
    }

    [Fact]
    public void Algiers_bounds_are_inclusive_at_the_start_and_exclusive_at_the_end()
    {
        var resolved = CustomerStatementWindowRules.TryResolve(
            "2026-11-01",
            "2026-11-02",
            "Africa/Algiers",
            November,
            out var window,
            out var error);

        Assert.True(resolved);
        Assert.Null(error);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), window.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.Zero), window.ToUtc);
        Assert.True(CustomerStatementWindowRules.Includes(window.FromUtc, window.FromUtc, window.ToUtc));
        Assert.False(CustomerStatementWindowRules.Includes(window.ToUtc, window.FromUtc, window.ToUtc));
    }

    [Fact]
    public void A_july_algiers_date_stays_one_hour_ahead_of_utc()
    {
        var resolved = CustomerStatementWindowRules.TryResolve(
            "2026-07-01",
            "2026-07-02",
            "Africa/Algiers",
            November,
            out var window,
            out _);

        Assert.True(resolved);
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero), window.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero), window.ToUtc);
    }

    [Fact]
    public void Equal_dates_are_an_empty_period()
    {
        var resolved = CustomerStatementWindowRules.TryResolve("2026-11-01", "2026-11-01", "Africa/Algiers", November, out var window, out var error);

        Assert.True(resolved);
        Assert.Null(error);
        Assert.Equal(window.FromUtc, window.ToUtc);
    }

    [Theory]
    [InlineData("2026-11-01", null, "to")]
    [InlineData(null, "2026-11-02", "from")]
    [InlineData("2026-13-01", "2026-11-02", "from")]
    [InlineData("01/11/2026", "2026-11-02", "from")]
    public void A_partial_or_invalid_date_is_rejected(string? from, string? to, string field)
    {
        var resolved = CustomerStatementWindowRules.TryResolve(from, to, "Africa/Algiers", November, out _, out var error);

        Assert.False(resolved);
        Assert.Equal(field, error!.Field);
        Assert.Equal("statement.date", error.Code);
    }

    [Fact]
    public void A_reversed_range_is_rejected()
    {
        var resolved = CustomerStatementWindowRules.TryResolve("2026-11-02", "2026-11-01", "Africa/Algiers", November, out _, out var error);

        Assert.False(resolved);
        Assert.Equal("to", error!.Field);
        Assert.Equal("statement.range", error.Code);
    }

    [Fact]
    public void An_unknown_timezone_is_rejected()
    {
        var resolved = CustomerStatementWindowRules.TryResolve(null, null, "Not/AZone", November, out _, out var error);

        Assert.False(resolved);
        Assert.Equal("timeZoneId.invalid", error!.Code);
    }

    [Fact]
    public void An_empty_history_keeps_the_opening_as_the_closing_balance()
    {
        var projected = CustomerStatementMath.TryProject(10.0000m, [], out var closing, out var running);

        Assert.True(projected);
        Assert.Equal(10.0000m, closing);
        Assert.Empty(running);
    }

    [Fact]
    public void Sales_increase_the_balance_and_payments_and_returns_decrease_it()
    {
        var when = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var sale = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var returned = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var payment = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var movements = new CustomerStatementMovement[]
        {
            new(when, "payment", payment, -4.0000m),
            new(when, "sale", sale, 10.0000m),
            new(when, "return", returned, -1.5000m),
        };

        var projected = CustomerStatementMath.TryProject(6.0000m, movements, out var closing, out var running);

        Assert.True(projected);
        Assert.Equal([16.0000m, 14.5000m, 10.5000m], running);
        Assert.Equal(10.5000m, closing);
    }

    [Fact]
    public void The_same_instant_orders_by_kind_and_then_document_text()
    {
        var when = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var earlier = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var later = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var movements = new CustomerStatementMovement[]
        {
            new(when, "sale", later, 2.0000m),
            new(when, "sale", earlier, 1.0000m),
        };

        var projected = CustomerStatementMath.TryProject(0m, movements, out var closing, out var running);

        Assert.True(projected);
        Assert.Equal([1.0000m, 3.0000m], running);
        Assert.Equal(3.0000m, closing);
        Assert.True(string.CompareOrdinal(earlier.ToString("D"), later.ToString("D")) < 0);
    }

    [Fact]
    public void A_negative_running_step_is_kept_when_the_period_closes_at_zero_or_above()
    {
        var when = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
        var later = when.AddHours(1);
        var movements = new CustomerStatementMovement[]
        {
            new(later, "sale", Guid.Parse("00000000-0000-0000-0000-000000000002"), 5.0000m),
            new(when, "payment", Guid.Parse("00000000-0000-0000-0000-000000000001"), -1.0000m),
        };

        var projected = CustomerStatementMath.TryProject(0m, movements, out var closing, out var running);

        Assert.True(projected);
        Assert.Equal([-1.0000m, 4.0000m], running);
        Assert.Equal(4.0000m, closing);
    }

    [Theory]
    [InlineData("-0.0001", "0")]
    [InlineData("1.0000", "-2.0000")]
    public void A_negative_opening_or_closing_is_rejected(string openingText, string movementText)
    {
        var opening = decimal.Parse(openingText, System.Globalization.CultureInfo.InvariantCulture);
        var movement = decimal.Parse(movementText, System.Globalization.CultureInfo.InvariantCulture);
        var movements = movement == 0m
            ? []
            : new CustomerStatementMovement[]
            {
                new(November, "payment", Guid.Parse("00000000-0000-0000-0000-000000000001"), movement),
            };

        var projected = CustomerStatementMath.TryProject(opening, movements, out _, out _);

        Assert.False(projected);
    }
}
