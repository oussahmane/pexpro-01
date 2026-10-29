namespace Pexilio.Domain.Catalog;

public sealed class Category
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = "";

    public string NameKey { get; set; } = "";
}
