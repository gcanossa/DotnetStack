using GKit.UI;
using GKit.UI.Data;

namespace Test.Repo.UI;

/// <summary>
/// Covers the ordering extensions extracted out of the MudBlazor adapter. These used to be
/// reachable only through a rendered grid; they are plain queryable extensions now.
/// </summary>
public class QueryOrderExtensionsTest
{
  private class Node
  {
    public Node? Child { get; set; }
    public string? Name { get; set; }
    public int Value { get; set; }
  }

  private static IQueryable<Node> Query(params Node[] nodes) => nodes.AsQueryable();

  [Fact]
  public void OrderBy_NoSorts_ReturnsSourceUnchanged()
  {
    var source = Query(new Node { Value = 2 }, new Node { Value = 1 });

    var result = source.OrderBy([]);

    Assert.Same(source, result);
  }

  [Fact]
  public void OrderBy_SingleAscendingPath_SortsAscending()
  {
    var source = Query(
      new Node { Value = 3 },
      new Node { Value = 1 },
      new Node { Value = 2 });

    var result = source.OrderBy([new GridSort("Value", false)]).ToList();

    Assert.Equal([1, 2, 3], result.Select(n => n.Value));
  }

  [Fact]
  public void OrderBy_Descending_SortsDescending()
  {
    var source = Query(
      new Node { Value = 1 },
      new Node { Value = 3 },
      new Node { Value = 2 });

    var result = source.OrderBy([new GridSort("Value", true)]).ToList();

    Assert.Equal([3, 2, 1], result.Select(n => n.Value));
  }

  [Fact]
  public void OrderBy_MultipleSorts_ChainsThenBy()
  {
    var source = Query(
      new Node { Name = "b", Value = 1 },
      new Node { Name = "a", Value = 2 },
      new Node { Name = "a", Value = 1 });

    var result = source.OrderBy([new GridSort("Name", false), new GridSort("Value", true)]).ToList();

    Assert.Equal([("a", 2), ("a", 1), ("b", 1)], result.Select(n => (n.Name!, n.Value)));
  }

  [Fact]
  public void OrderBy_NestedPath_SortsByLeaf()
  {
    var source = Query(
      new Node { Child = new Node { Value = 2 } },
      new Node { Child = new Node { Value = 1 } });

    var result = source.OrderBy([new GridSort("Child.Value", false)]).ToList();

    Assert.Equal([1, 2], result.Select(n => n.Child!.Value));
  }

  [Fact]
  public void OrderBy_NestedPathWithNull_Throws()
  {
    // Establishes the behaviour NullCheckingOrderBy exists to avoid.
    var source = Query(new Node { Child = null }, new Node { Child = new Node { Value = 1 } });

    Assert.ThrowsAny<Exception>(() => source.OrderBy([new GridSort("Child.Value", false)]).ToList());
  }

  [Fact]
  public void NullCheckingOrderBy_NestedPathWithNull_TreatsNullAsDefault()
  {
    var source = Query(
      new Node { Child = new Node { Value = 5 } },
      new Node { Child = null },
      new Node { Child = new Node { Value = 3 } });

    var result = source.NullCheckingOrderBy([new GridSort("Child.Value", false)]).ToList();

    // The null child sorts as default(int) == 0, so it comes first ascending.
    Assert.Null(result[0].Child);
    Assert.Equal([3, 5], result.Skip(1).Select(n => n.Child!.Value));
  }

  [Fact]
  public void NullCheckingOrderBy_DeepPathWithIntermediateNulls_DoesNotThrow()
  {
    var source = Query(
      new Node { Child = new Node { Child = new Node { Child = new Node { Value = 2 } } } },
      new Node { Child = new Node { Child = null } },
      new Node { Child = null },
      new Node { Child = new Node { Child = new Node { Child = new Node { Value = 1 } } } });

    var result = source.NullCheckingOrderBy([new GridSort("Child.Child.Child.Value", false)]).ToList();

    Assert.Equal(4, result.Count);
    // Both null-bearing rows collapse to 0 and precede the real values.
    Assert.Equal([0, 0, 1, 2], result.Select(n => n.Child?.Child?.Child?.Value ?? 0));
  }

  [Fact]
  public void NullCheckingOrderBy_SinglePath_BehavesLikeOrderBy()
  {
    var source = Query(new Node { Value = 2 }, new Node { Value = 1 });

    var result = source.NullCheckingOrderBy([new GridSort("Value", false)]).ToList();

    Assert.Equal([1, 2], result.Select(n => n.Value));
  }

  [Fact]
  public void NullCheckingOrderBy_ReferenceLeaf_OrdersNullsFirst()
  {
    var source = Query(
      new Node { Child = new Node { Name = "b" } },
      new Node { Child = null },
      new Node { Child = new Node { Name = "a" } });

    var result = source.NullCheckingOrderBy([new GridSort("Child.Name", false)]).ToList();

    Assert.Null(result[0].Child);
    Assert.Equal(["a", "b"], result.Skip(1).Select(n => n.Child!.Name!));
  }
}
