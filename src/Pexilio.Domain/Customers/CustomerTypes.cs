namespace Pexilio.Domain.Customers;

public static class CustomerTypes
{
    public const string Individual = "individual";

    public const string Business = "business";

    public static readonly string[] All = [Individual, Business];
}

public static class CustomerOperations
{
    public const string Create = "customer.create";
}
