using NPOI.SS.Formula.Functions;

namespace GKit.Reporting;

public static class GroupingExtensions
{
  public static IEnumerable<GroupingItem<T>> ExecuteGrouping<T>(this IEnumerable<GroupingColumnDescriptor<T>> ext, IEnumerable<T> data)
  {
    var root = new GroupingItem<T> { Label = "root", Value = "root", Items = data };
    IEnumerable<GroupingItem<T>> leafs = [root];
    foreach (var def in ext)
    {
      List<GroupingItem<T>> currentLeafs = [];
      foreach (var leaf in leafs)
      {
        leaf.Children = [.. leaf.Items!.GroupBy(def.SelectValue).Select(p => new GroupingItem<T>
        {
          Label = def?.Label ?? "",
          Value = p.Key.ToString()!,
          Items = p
        })];
        currentLeafs.AddRange(leaf.Children);
      }

      leafs = currentLeafs;
    }

    return root.Children;
  }
}
