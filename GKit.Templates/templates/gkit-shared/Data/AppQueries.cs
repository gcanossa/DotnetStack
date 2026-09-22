using Microsoft.EntityFrameworkCore;

namespace GKit.Shared1.Data;

/// <summary>
/// Query factories the grids bind to. Keeping them here rather than in the host is what makes the
/// data plumbing survive a change of UI library: an EntityGrid takes a
/// <c>Func&lt;DbContext, IQueryable&lt;T&gt;&gt;</c> and neither adapter appears in that signature.
/// </summary>
public static class AppQueries
{
  // Example shape - the .Include().WithoutDeleted() chains go here.
  // public static IQueryable<Vehicle> Vehicles(DbContext context) =>
  //   context.Set<Vehicle>().Include(p => p.Owner).WithoutDeleted();
}
