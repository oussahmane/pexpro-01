using Pexilio.Domain.Catalog;
using Pexilio.Domain.Stock;

namespace Pexilio.Domain.Sales;

public readonly record struct SaleLineDraft(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string PriceKind = PriceKinds.Retail,
    decimal LineDiscount = 0m,
    string DiscountKind = DiscountKinds.None,
    decimal? DiscountRate = null);

public readonly record struct SalePaymentDraft(string Method, decimal Amount);

public readonly record struct SaleDraft(
    IReadOnlyList<SaleLineDraft> Lines,
    decimal Total,
    IReadOnlyList<SalePaymentDraft> Payments,
    decimal DiscountAmount = 0m,
    string DiscountKind = DiscountKinds.None,
    decimal? DiscountRate = null,
    bool Extended = false);

public readonly record struct SaleLineInput(
    Guid ProductId,
    string? Quantity,
    string? UnitPrice,
    string? LineTotal,
    string? PriceKind,
    string? LineDiscount,
    string? DiscountKind,
    string? DiscountRate);

public static class SaleValidator
{
    public static bool TryCashSale(
        IReadOnlyList<(Guid ProductId, string? Quantity, string? UnitPrice, string? LineTotal)>? lines,
        string? totalText,
        IReadOnlyList<(string? Method, string? Amount)>? payments,
        out SaleDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var inputs = lines?.Select(line => new SaleLineInput(
            line.ProductId,
            line.Quantity,
            line.UnitPrice,
            line.LineTotal,
            null,
            null,
            null,
            null)).ToArray();
        return TryCashSale(inputs, totalText, payments, null, null, null, false, out draft, out errors);
    }

    public static bool TryCashSale(
        IReadOnlyList<SaleLineInput>? lines,
        string? totalText,
        IReadOnlyList<(string? Method, string? Amount)>? payments,
        string? discountAmountText,
        string? discountKindText,
        string? discountRateText,
        out SaleDraft draft,
        out IReadOnlyList<FieldError> errors) =>
        TryCashSale(lines, totalText, payments, discountAmountText, discountKindText, discountRateText, false, out draft, out errors);

    public static bool TryCashSale(
        IReadOnlyList<SaleLineInput>? lines,
        string? totalText,
        IReadOnlyList<(string? Method, string? Amount)>? payments,
        string? discountAmountText,
        string? discountKindText,
        string? discountRateText,
        bool customerLinked,
        out SaleDraft draft,
        out IReadOnlyList<FieldError> errors)
    {
        var found = new List<FieldError>();
        var extended = discountAmountText is not null
            || discountKindText is not null
            || discountRateText is not null
            || (lines?.Any(line => line.PriceKind is not null
                || line.LineDiscount is not null
                || line.DiscountKind is not null
                || line.DiscountRate is not null) ?? false);
        var parsedLines = new List<SaleLineDraft>();
        if (lines is null || lines.Count == 0)
        {
            found.Add(new FieldError("lines", "lines.required"));
        }
        else if (!SaleRules.OneProduct(lines.Select(line => line.ProductId)))
        {
            found.Add(new FieldError("lines", "lines.duplicate"));
        }
        else
        {
            foreach (var line in lines)
            {
                if (!TryLine(line, extended, found, out var parsed))
                {
                    continue;
                }

                parsedLines.Add(parsed);
            }
        }

        var saleTotalOk = QuantityText.TryParse(totalText, "total", false, out var total, out var saleTotalCode);
        if (!saleTotalOk)
        {
            found.Add(new FieldError("total", saleTotalCode));
        }

        if (!TrySaleDiscount(extended, discountKindText, discountRateText, discountAmountText, parsedLines, found, out var discountKind, out var discountRate, out var discountAmount))
        {
            discountKind = DiscountKinds.None;
            discountRate = null;
            discountAmount = 0m;
        }

        var parsedPayments = ParsePayments(payments, customerLinked, found);
        if (saleTotalOk && total <= 0m)
        {
            found.Add(new FieldError("total", "total.positive"));
        }

        if (found.Count == 0 && saleTotalOk)
        {
            var lineSum = parsedLines.Sum(line => line.LineTotal);
            if (lineSum - discountAmount != total)
            {
                found.Add(new FieldError("total", "total.mismatch"));
            }

            var paid = parsedPayments.Sum(payment => payment.Amount);
            if ((!customerLinked && paid != total) || (customerLinked && paid > total))
            {
                found.Add(new FieldError("payments", "payment.total"));
            }
        }

        if (found.Count > 0 || !saleTotalOk)
        {
            draft = default;
            errors = found;
            return false;
        }

        draft = new SaleDraft(parsedLines, total, parsedPayments, discountAmount, discountKind, discountRate, extended);
        errors = found;
        return true;
    }

    private static bool TryLine(SaleLineInput line, bool extended, List<FieldError> found, out SaleLineDraft parsed)
    {
        parsed = default;
        if (line.ProductId == Guid.Empty)
        {
            found.Add(new FieldError("lines", "lines.product"));
        }

        var quantityOk = QuantityText.TryParse(line.Quantity, "quantity", false, out var quantity, out var quantityCode);
        if (!quantityOk)
        {
            found.Add(new FieldError("quantity", quantityCode));
        }
        else if (quantity <= 0m)
        {
            found.Add(new FieldError("quantity", "quantity.positive"));
        }

        var priceOk = UnitPriceText.TryParse(line.UnitPrice, out var unitPrice, out var priceCode);
        if (!priceOk)
        {
            found.Add(new FieldError("unitPrice", priceCode));
        }

        var totalOk = QuantityText.TryParse(line.LineTotal, "lineTotal", false, out var lineTotal, out var lineTotalCode);
        if (!totalOk)
        {
            found.Add(new FieldError("lineTotal", lineTotalCode));
        }

        var priceKind = PriceKinds.Retail;
        if (extended && line.PriceKind is not null)
        {
            priceKind = line.PriceKind.Trim().ToLowerInvariant();
            if (!PriceKinds.IsKnown(priceKind))
            {
                found.Add(new FieldError("priceKind", "price.kind"));
                priceOk = false;
            }
        }

        if (!quantityOk || quantity <= 0m || !priceOk || !totalOk || line.ProductId == Guid.Empty)
        {
            return false;
        }

        if (!SaleRules.TryGross(quantity, unitPrice, out var gross))
        {
            found.Add(new FieldError("lineTotal", "line.scale"));
            return false;
        }

        if (!TryLineDiscount(extended, line, gross, found, out var discountKind, out var discountRate, out var lineDiscount))
        {
            return false;
        }

        if (gross - lineDiscount != lineTotal)
        {
            found.Add(new FieldError("lineTotal", "lineTotal.mismatch"));
            return false;
        }

        parsed = new SaleLineDraft(line.ProductId, quantity, unitPrice, lineTotal, priceKind, lineDiscount, discountKind, discountRate);
        return true;
    }

    private static bool TryLineDiscount(
        bool extended,
        SaleLineInput line,
        decimal gross,
        List<FieldError> found,
        out string kind,
        out decimal? rate,
        out decimal amount)
    {
        kind = DiscountKinds.None;
        rate = null;
        amount = 0m;
        if (!extended)
        {
            return true;
        }

        if (line.DiscountKind is not null)
        {
            kind = line.DiscountKind.Trim().ToLowerInvariant();
            if (!DiscountKinds.IsKnown(kind))
            {
                found.Add(new FieldError("discountKind", "discount.kind"));
                return false;
            }
        }

        if (kind == DiscountKinds.Percent)
        {
            if (!SaleRules.TryRate(line.DiscountRate, out var parsedRate, out var rateCode))
            {
                found.Add(new FieldError("discountRate", rateCode));
                return false;
            }

            rate = parsedRate;
        }
        else if (line.DiscountRate is not null)
        {
            found.Add(new FieldError("discountRate", "discount.rate"));
            return false;
        }

        if (line.LineDiscount is null)
        {
            amount = 0m;
        }
        else if (!UnitPriceText.TryParse(line.LineDiscount, out amount, out var discountCode))
        {
            found.Add(new FieldError("lineDiscount", discountCode.Replace("unitPrice", "lineDiscount", StringComparison.Ordinal)));
            return false;
        }

        if (kind == DiscountKinds.Amount && line.LineDiscount is not null && amount > gross)
        {
            found.Add(new FieldError("lineDiscount", "discount.exceeds"));
            return false;
        }

        if (!SaleRules.TryDiscountAmount(gross, kind, rate, amount, out var accepted))
        {
            found.Add(new FieldError("lineDiscount", amount > gross ? "discount.exceeds" : "discount.mismatch"));
            return false;
        }

        amount = accepted;
        return true;
    }

    private static bool TrySaleDiscount(
        bool extended,
        string? kindText,
        string? rateText,
        string? amountText,
        List<SaleLineDraft> lines,
        List<FieldError> found,
        out string kind,
        out decimal? rate,
        out decimal amount)
    {
        kind = DiscountKinds.None;
        rate = null;
        amount = 0m;
        if (!extended)
        {
            return true;
        }

        if (kindText is not null)
        {
            kind = kindText.Trim().ToLowerInvariant();
            if (!DiscountKinds.IsKnown(kind))
            {
                found.Add(new FieldError("discountKind", "discount.kind"));
                return false;
            }
        }

        if (kind == DiscountKinds.Percent)
        {
            if (!SaleRules.TryRate(rateText, out var parsedRate, out var rateCode))
            {
                found.Add(new FieldError("discountRate", rateCode));
                return false;
            }

            rate = parsedRate;
        }
        else if (rateText is not null)
        {
            found.Add(new FieldError("discountRate", "discount.rate"));
            return false;
        }

        if (amountText is null)
        {
            amount = 0m;
        }
        else if (!UnitPriceText.TryParse(amountText, out amount, out var amountCode))
        {
            found.Add(new FieldError("discountAmount", amountCode.Replace("unitPrice", "discountAmount", StringComparison.Ordinal)));
            return false;
        }

        var basis = lines.Sum(line => line.LineTotal);
        if (!SaleRules.TryDiscountAmount(basis, kind, rate, amount, out var accepted))
        {
            found.Add(new FieldError("discountAmount", amount > basis ? "discount.exceeds" : "discount.mismatch"));
            return false;
        }

        amount = accepted;
        return true;
    }

    private static List<SalePaymentDraft> ParsePayments(
        IReadOnlyList<(string? Method, string? Amount)>? payments,
        bool customerLinked,
        List<FieldError> found)
    {
        var parsedPayments = new List<SalePaymentDraft>();
        if (payments is null || payments.Count == 0)
        {
            if (!customerLinked)
            {
                found.Add(new FieldError("payments", "payments.required"));
            }

            return parsedPayments;
        }

        if (payments.Count > 1)
        {
            found.Add(new FieldError("payments", "payments.single"));
        }

        foreach (var payment in payments)
        {
            var method = payment.Method?.Trim().ToLowerInvariant() ?? "";
            var accepted = customerLinked
                ? SaleRules.PaymentMethods.Contains(method)
                : method == SaleRules.CashMethod;
            if (!accepted)
            {
                found.Add(new FieldError("payments", "payment.method"));
            }

            var amountOk = QuantityText.TryParse(payment.Amount, "payment", false, out var amount, out var amountCode);
            if (!amountOk)
            {
                found.Add(new FieldError("payment", amountCode));
            }
            else if (amount <= 0m)
            {
                found.Add(new FieldError("payment", "payment.positive"));
            }
            else if (accepted)
            {
                parsedPayments.Add(new SalePaymentDraft(method, amount));
            }
        }

        return parsedPayments;
    }
}
