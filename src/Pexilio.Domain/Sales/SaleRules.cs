using System.Numerics;
using Pexilio.Domain.Catalog;
using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Sales;

public static class PriceKinds
{
    public const string Retail = "retail";

    public const string Wholesale = "wholesale";

    public static bool IsKnown(string? kind) => kind is Retail or Wholesale;
}

public static class DiscountKinds
{
    public const string None = "none";

    public const string Amount = "amount";

    public const string Percent = "percent";

    public static bool IsKnown(string? kind) => kind is None or Amount or Percent;
}

public static class SaleRules
{
    public const string CashMethod = "cash";

    public const int Scale = 4;

    public static readonly string[] PaymentMethods = ["cash", "transfer", "card", "other"];

    public static bool OneProduct(IEnumerable<Guid> productIds)
    {
        var seen = new HashSet<Guid>();
        foreach (var productId in productIds)
        {
            if (!seen.Add(productId))
            {
                return false;
            }
        }

        return true;
    }

    public static decimal SaleDelta(decimal quantity) => -quantity;

    public static decimal ReturnDelta(decimal quantity) => quantity;

    public static bool ExactlyScale4(decimal value) =>
        value == decimal.Round(value, Scale, MidpointRounding.AwayFromZero);

    public static bool TryGross(decimal quantity, decimal unitPrice, out decimal gross)
    {
        gross = 0m;
        if (quantity <= 0m || unitPrice < 0m || !ExactlyScale4(quantity) || !ExactlyScale4(unitPrice))
        {
            return false;
        }

        var product = quantity * unitPrice;
        if (!ExactlyScale4(product) || product < 0m)
        {
            return false;
        }

        gross = product;
        return true;
    }

    public static bool TryRate(string? text, out decimal rate, out string code)
    {
        if (!UnitPriceText.TryParse(text, out rate, out var priceCode))
        {
            code = priceCode switch
            {
                "unitPrice.required" => "discountRate.required",
                "unitPrice.invalid" => "discountRate.invalid",
                "unitPrice.scale" => "discountRate.scale",
                _ => "discount.rate",
            };
            return false;
        }

        if (rate > 100m)
        {
            code = "discount.rate";
            return false;
        }

        code = "";
        return true;
    }

    public static bool TryPercentAmount(decimal basis, decimal rate, out decimal amount)
    {
        amount = 0m;
        if (basis < 0m || rate < 0m || rate > 100m || !ExactlyScale4(basis) || !ExactlyScale4(rate))
        {
            return false;
        }

        var product = Scaled(basis) * Scaled(rate);
        var divisor = new BigInteger(1_000_000);
        var quotient = BigInteger.DivRem(product, divisor, out var remainder);
        if (remainder * 2 >= divisor)
        {
            quotient += BigInteger.One;
        }

        if (quotient < 0 || quotient > Scaled(QuantityText.Maximum))
        {
            return false;
        }

        amount = ToMoney(quotient);
        return amount <= basis;
    }

    // Sale and sale-line checks accept a percentage rate from 0 through 100.
    // They do not recompute the stored amount from that rate. This method is the authority for the amount.
    public static bool TryDiscountAmount(
        decimal basis,
        string kind,
        decimal? rate,
        decimal submitted,
        out decimal amount)
    {
        amount = 0m;
        if (!ExactlyScale4(basis) || basis < 0m || !DiscountKinds.IsKnown(kind))
        {
            return false;
        }

        if (kind == DiscountKinds.None)
        {
            return rate is null && submitted == 0m;
        }

        if (kind == DiscountKinds.Amount)
        {
            if (rate is not null || submitted < 0m || submitted > basis || !ExactlyScale4(submitted))
            {
                return false;
            }

            amount = submitted;
            return true;
        }

        if (rate is not decimal percent || !TryPercentAmount(basis, percent, out var computed) || computed != submitted)
        {
            return false;
        }

        amount = computed;
        return true;
    }

    public static bool TryAllocate(IReadOnlyList<decimal> lineTotals, decimal discount, out IReadOnlyList<decimal> allocations)
    {
        allocations = [];
        if (discount < 0m || !ExactlyScale4(discount) || lineTotals.Any(total => total < 0m || !ExactlyScale4(total)))
        {
            return false;
        }

        var totals = lineTotals.Select(Scaled).ToArray();
        var discountScaled = Scaled(discount);
        var subtotal = totals.Aggregate(BigInteger.Zero, (sum, total) => sum + total);
        if (discountScaled > subtotal || (subtotal.IsZero && !discountScaled.IsZero))
        {
            return false;
        }

        if (discountScaled.IsZero)
        {
            allocations = lineTotals.Select(_ => 0m).ToArray();
            return true;
        }

        var quotients = new BigInteger[totals.Length];
        var remainders = new BigInteger[totals.Length];
        var assigned = BigInteger.Zero;
        for (var index = 0; index < totals.Length; index++)
        {
            quotients[index] = BigInteger.DivRem(totals[index] * discountScaled, subtotal, out var remainder);
            remainders[index] = remainder;
            assigned += quotients[index];
        }

        var leftover = discountScaled - assigned;
        var order = Enumerable.Range(0, totals.Length)
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => index)
            .ToArray();
        foreach (var index in order)
        {
            if (leftover.IsZero)
            {
                break;
            }

            quotients[index] += BigInteger.One;
            leftover -= BigInteger.One;
        }

        if (!leftover.IsZero || quotients.Select((share, index) => share > totals[index]).Any(exceeded => exceeded))
        {
            return false;
        }

        var shares = quotients.Select(ToMoney).ToArray();
        if (shares.Sum() != discount)
        {
            return false;
        }

        allocations = shares;
        return true;
    }

    public static bool NetBelowFloor(decimal net, decimal quantity, decimal floorUnit)
    {
        if (net < 0m || quantity <= 0m || floorUnit < 0m || !ExactlyScale4(net) || !ExactlyScale4(quantity) || !ExactlyScale4(floorUnit))
        {
            return false;
        }

        return Scaled(net) * new BigInteger(10_000) < Scaled(quantity) * Scaled(floorUnit);
    }

    private static BigInteger Scaled(decimal value) => new(value * 10000m);

    private static decimal ToMoney(BigInteger scaled) => (decimal)scaled / 10000m;
}
