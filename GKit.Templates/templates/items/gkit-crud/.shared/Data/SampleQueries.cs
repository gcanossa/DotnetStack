using Microsoft.EntityFrameworkCore;

namespace GKIT-SHARED-NS;

/// <summary>
/// The part of a CRUD screen that survives a change of UI library: EntityGrid takes a
/// <c>Func&lt;DbContext, IQueryable&lt;T&gt;&gt;</c>, and nothing here names a component library.
/// Put the .Include() chains in <see cref="Query"/>.
/// </summary>
/// <remarks>
/// Soft deleted rows are already excluded: WithSoftDelete() in OnModelCreating installs a global
/// query filter, so this query does not need to repeat it. Use IgnoreQueryFilters() on the rare
/// screen that has to show them.
/// </remarks>
public static class SampleQueries
{
    public static IQueryable<Sample> Query(DbContext context) =>
        context.Set<Sample>();

    public static Sample Empty() => new()
    {
        Name = "",
        CreatedAt = DateTime.UtcNow
    };

    public static string Describe(Sample item) => item.Name;
}
