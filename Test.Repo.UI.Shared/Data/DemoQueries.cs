using Microsoft.EntityFrameworkCore;

namespace Test.Repo.UI.Shared;

/// <summary>
/// Query factories and entity defaults for the demo.
/// </summary>
/// <remarks>
/// This is the code the dual-adapter design is meant to keep portable. It is handed unchanged to
/// <c>EntityGrid.QueryFactory</c> under either adapter, and nothing here names a component
/// library.
/// </remarks>
public static class DemoQueries
{
  public static IQueryable<Widget> Widgets(DbContext ctx) =>
    ctx.Set<Widget>().Include(w => w.Category);

  public static IQueryable<Category> Categories(DbContext ctx) =>
    ctx.Set<Category>();

  public static Func<string, System.Linq.Expressions.Expression<Func<Category, bool>>> CategorySearch =>
    text => c => c.Name.Contains(text);

  public static Widget EmptyWidget() => new()
  {
    Name = "",
    Quantity = 0,
    CreatedAt = DateTime.UtcNow
  };

  public static Category EmptyCategory() => new()
  {
    Name = "",
    Active = true
  };

  public static string DescribeWidget(Widget widget) => widget.Name;

  public static string DescribeCategory(Category category) => category.Name;
}
