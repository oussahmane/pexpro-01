using Pexilio.Domain;

namespace Pexilio.Domain.Sales;

public static class SaleCustomerLink
{
    public const string Invalid = "customer.invalid";

    public static string? Problem(Guid? customerId, bool found, bool active)
    {
        if (customerId is null)
        {
            return null;
        }

        if (customerId == Guid.Empty)
        {
            return Invalid;
        }

        if (!found)
        {
            return ErrorCodes.CustomerNotFound;
        }

        if (!active)
        {
            return ErrorCodes.CustomerInactive;
        }

        return null;
    }
}
