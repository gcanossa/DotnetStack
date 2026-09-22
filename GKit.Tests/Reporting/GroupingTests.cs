using GKit.Reporting;

namespace GKit.Tests.Reporting;

public class GroupingTests
{
  public class Parent
  {
    public int Region { get; set; }
    public string Name { get; set; } = "";
    public Child Child { get; set; } = new();
  }

  public class Child
  {
    public int Zone { get; set; }
  }

  private static IEnumerable<GroupingColumnDescriptor<Parent>> Groups(
    params (string Label, Func<Parent, object> Selector)[] specs) =>
    specs.Select(s => new GroupingColumnDescriptor<Parent, object>(s.Label, s.Selector));

  [Fact]
  public void Groups_by_a_single_key()
  {
    IEnumerable<Parent> data =
    [
      new() { Region = 1 }, new() { Region = 1 }, new() { Region = 2 }
    ];

    var result = Groups(("Region", p => p.Region)).ExecuteGrouping(data).ToList();

    Assert.Equal(2, result.Count);
    Assert.Equal(2, result[0].Items.Count());
    Assert.Single(result[1].Items);
  }

  [Fact]
  public void Groups_by_nested_keys()
  {
    IEnumerable<Parent> data =
    [
      new() { Region = 1, Child = new() { Zone = 1 } },
      new() { Region = 1, Child = new() { Zone = 2 } },
      new() { Region = 2, Child = new() { Zone = 3 } }
    ];

    var result = Groups(("Region", p => p.Region), ("Zone", p => p.Child.Zone))
      .ExecuteGrouping(data).ToList();

    Assert.Equal(2, result.Count);
    Assert.Equal(2, result[0].Children.Count());
    Assert.Single(result[1].Children);
  }

  [Fact]
  public void Reports_the_group_label_and_value()
  {
    IEnumerable<Parent> data = [new() { Region = 7 }];

    var group = Groups(("Region", p => p.Region)).ExecuteGrouping(data).Single();

    Assert.Equal("Region", group.Label);
    Assert.Equal("7", group.Value.ToString());
  }

  [Fact]
  public void Depth_reflects_the_number_of_grouping_levels()
  {
    IEnumerable<Parent> data = [new() { Region = 1, Child = new() { Zone = 1 } }];

    var oneLevel = Groups(("Region", p => p.Region)).ExecuteGrouping(data).Single();
    var twoLevels = Groups(("Region", p => p.Region), ("Zone", p => p.Child.Zone))
      .ExecuteGrouping(data).Single();

    Assert.Equal(1, oneLevel.Depth);
    Assert.Equal(2, twoLevels.Depth);
  }

  [Fact]
  public void AllSubNodes_enumerates_the_whole_subtree()
  {
    IEnumerable<Parent> data =
    [
      new() { Region = 1, Child = new() { Zone = 1 } },
      new() { Region = 1, Child = new() { Zone = 2 } }
    ];

    var group = Groups(("Region", p => p.Region), ("Zone", p => p.Child.Zone))
      .ExecuteGrouping(data).Single();

    Assert.Equal(2, group.AllSubNodes.Count());
  }

  [Fact]
  public void An_empty_data_set_produces_no_groups()
  {
    var result = Groups(("Region", p => p.Region)).ExecuteGrouping([]).ToList();

    Assert.Empty(result);
  }

  [Fact]
  public void Null_group_keys_do_not_throw()
  {
    // GroupingExtensions calls p.Key.ToString()! on the group key.
    IEnumerable<Parent> data = [new() { Name = null! }];

    var ex = Record.Exception(() =>
      Groups(("Name", p => p.Name)).ExecuteGrouping(data).ToList());

    Assert.Null(ex);
  }

  [Fact]
  public void Grouping_does_not_re_enumerate_a_single_pass_source()
  {
    // GroupingItem.Items is IEnumerable<T>; the XLS grouping reporter calls Count() and
    // AllSubNodes repeatedly, so a lazily-enumerated source is walked many times.
    var enumerations = 0;
    IEnumerable<Parent> Source()
    {
      enumerations++;
      yield return new Parent { Region = 1 };
      yield return new Parent { Region = 2 };
    }

    var groups = Groups(("Region", p => p.Region)).ExecuteGrouping(Source()).ToList();
    _ = groups.Sum(g => g.Items.Count());
    _ = groups.Sum(g => g.Items.Count());

    Assert.Equal(1, enumerations);
  }
}
