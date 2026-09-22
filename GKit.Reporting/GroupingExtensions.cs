namespace GKit.Reporting;

public static class GroupingExtensions
{
  public static IEnumerable<GroupingItem<T>> ExecuteGrouping<T>(
    this IEnumerable<GroupingColumnDescriptor<T>> ext, IEnumerable<T> data)
  {
    ArgumentNullException.ThrowIfNull(ext);
    ArgumentNullException.ThrowIfNull(data);

    // Enumerate the source exactly once: it may be a query or a single-pass sequence.
    var root = new GroupingItem<T> { Label = "root", Value = "root", Items = [.. data] };

    IReadOnlyList<GroupingItem<T>> leafs = [root];

    foreach (var def in ext)
    {
      List<GroupingItem<T>> currentLeafs = [];

      foreach (var leaf in leafs)
      {
        leaf.Children =
        [
          .. leaf.Items
            .GroupBy(def.SelectValue)
            .Select(p => new GroupingItem<T>
            {
              Label = def.Label ?? "",
              // A null grouping key is legitimate data; p.Key.ToString()! threw on it.
              Value = p.Key ?? "",
              Items = [.. p],
              Parent = leaf
            })
        ];

        currentLeafs.AddRange(leaf.Children);
      }

      leafs = currentLeafs;
    }

    return root.Children;
  }
}
