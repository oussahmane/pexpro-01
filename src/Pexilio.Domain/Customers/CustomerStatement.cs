using System.Globalization;
using Pexilio.Domain;

namespace Pexilio.Domain.Customers;

public readonly record struct CustomerStatementWindow(
    DateOnly From,
    DateOnly To,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);

public readonly record struct CustomerStatementMovement(
    DateTimeOffset OccurredAt,
    string Kind,
    Guid DocumentId,
    decimal SignedAmount);

public static class CustomerStatementWindowRules
{
    public static bool Includes(DateTimeOffset instant, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        instant >= fromUtc && instant < toUtc;

    public static bool TryResolve(
        string? fromText,
        string? toText,
        string timeZoneId,
        DateTimeOffset utcNow,
        out CustomerStatementWindow window,
        out FieldError? error)
    {
        window = default;
        var fromBlank = string.IsNullOrWhiteSpace(fromText);
        var toBlank = string.IsNullOrWhiteSpace(toText);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
        {
            error = new FieldError("timeZoneId", "timeZoneId.invalid");
            return false;
        }

        DateOnly from;
        DateOnly to;
        if (fromBlank && toBlank)
        {
            var localNow = TimeZoneInfo.ConvertTime(utcNow, zone);
            from = new DateOnly(localNow.Year, localNow.Month, 1);
            to = from.AddMonths(1);
        }
        else if (fromBlank || !TryDate(fromText, out from))
        {
            error = new FieldError("from", "statement.date");
            return false;
        }
        else if (toBlank || !TryDate(toText, out to))
        {
            error = new FieldError("to", "statement.date");
            return false;
        }
        else if (from > to)
        {
            error = new FieldError("to", "statement.range");
            return false;
        }

        if (!TryMidnight(from, zone, out var fromUtc) || !TryMidnight(to, zone, out var toUtc))
        {
            error = new FieldError("from", "statement.date");
            return false;
        }

        window = new CustomerStatementWindow(from, to, fromUtc, toUtc);
        error = null;
        return true;
    }

    private static bool TryDate(string? text, out DateOnly date)
    {
        var trimmed = text?.Trim() ?? "";
        return DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && trimmed == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static bool TryMidnight(DateOnly date, TimeZoneInfo zone, out DateTimeOffset utc)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            utc = default;
            return false;
        }

        try
        {
            utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
            return true;
        }
        catch (ArgumentException)
        {
            utc = default;
            return false;
        }
    }
}

public static class CustomerStatementMath
{
    public static int KindRank(string kind) => kind switch
    {
        "sale" => 0,
        "return" => 1,
        "payment" => 2,
        _ => 9,
    };

    public static bool TryProject(
        decimal opening,
        IReadOnlyList<CustomerStatementMovement> period,
        out decimal closing,
        out IReadOnlyList<decimal> running)
    {
        if (opening < 0m)
        {
            closing = 0m;
            running = [];
            return false;
        }

        var ordered = period
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => KindRank(item.Kind))
            .ThenBy(item => item.DocumentId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var balances = new decimal[ordered.Length];
        var cursor = opening;
        for (var index = 0; index < ordered.Length; index++)
        {
            cursor += ordered[index].SignedAmount;
            balances[index] = cursor;
        }

        closing = cursor;
        running = balances;
        return closing >= 0m;
    }
}
