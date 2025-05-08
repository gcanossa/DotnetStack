namespace GKit.Reporting;

public class GroupingItem<T>
{
  public IEnumerable<T> Items { get; internal set; } = [];
  public IEnumerable<GroupingItem<T>> Children { get; internal set; } = [];

  public GroupingItem<T>? Parent { get; internal set; } //TODO: use to avoid AllSubNodes and Depth operations

  public string Label { get; internal set; } = default!;
  public object Value { get; internal set; } = default!;

  public IEnumerable<GroupingItem<T>> AllSubNodes => Children.Union(Children.SelectMany(p => p.AllSubNodes));

  public int Depth => 1 + Children.Select(p => p.Depth).Union([0]).Max();
}