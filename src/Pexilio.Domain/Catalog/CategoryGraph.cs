namespace Pexilio.Domain.Catalog;

public static class CategoryGraph
{
    public static bool CreatesCycle(Guid categoryId, Guid? parentId, Func<Guid, Guid?> parentOf)
    {
        var cursor = parentId;
        var seen = 0;
        while (cursor is Guid current)
        {
            if (current == categoryId || seen++ > 64)
            {
                return true;
            }

            cursor = parentOf(current);
        }

        return false;
    }
}
