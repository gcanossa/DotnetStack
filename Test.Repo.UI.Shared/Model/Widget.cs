namespace Test.Repo.UI.Shared;

public class Category
{
  public int Id { get; set; }
  public string Name { get; set; } = "";
  public bool Active { get; set; } = true;
}

public class Widget
{
  public int Id { get; set; }
  public string Name { get; set; } = "";
  public string? Notes { get; set; }
  public int Quantity { get; set; }
  public DateTime CreatedAt { get; set; }

  /// <summary>
  /// Nullable on purpose: ordering a grid by <c>Category.Name</c> is what exercises
  /// <c>NullCheckingOrderBy</c>.
  /// </summary>
  public Category? Category { get; set; }
}
