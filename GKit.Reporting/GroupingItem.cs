namespace GKit.Reporting;

public class GroupingItem<T>
{
  /// <summary>
  /// Materialised deliberately: the XLS grouping reporter reads Items.Count() and AllSubNodes
  /// several times per node, so a lazy sequence was re-enumerated on every access.
  /// </summary>
  public IReadOnlyList<T> Items { get; internal set; } = [];

  public IReadOnlyList<GroupingItem<T>> Children { get; internal set; } = [];

  public GroupingItem<T>? Parent { get; internal set; }

  public string Label { get; internal set; } = default!;
  public object Value { get; internal set; } = default!;

  private IReadOnlyList<GroupingItem<T>>? _allSubNodes;
  private int? _depth;

  /// <summary>Every descendant, computed once. Was recursive and recomputed per access.</summary>
  public IReadOnlyList<GroupingItem<T>> AllSubNodes =>
    _allSubNodes ??= [.. Children, .. Children.SelectMany(p => p.AllSubNodes)];

  public int Depth => _depth ??= 1 + Children.Select(p => p.Depth).Append(0).Max();
}
