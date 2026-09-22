using GKit.EntityFramework;

namespace GKit.Tests.EntityFramework;

public class CopyExtensionsTests
{
  public class Plain
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
  }

  public class WithComputed
  {
    public string First { get; set; } = "";
    public string Last { get; set; } = "";

    // Get-only: reflection-based copying must skip it rather than throw.
    public string FullName => $"{First} {Last}";
  }

  public class Revisionable : IRevisionableEntity
  {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public RevisionInfo Revision { get; set; } = RevisionInfo.First();
    public object Clone() => new Revisionable { Id = Id, Name = Name, Revision = Revision.Clone() };
  }

  [Fact]
  public void CopyToObject_copies_every_writable_property()
  {
    var from = new Plain { Id = 1, Name = "a", Note = "n" };
    var to = new Plain();

    from.CopyToObject(to);

    Assert.Equal(1, to.Id);
    Assert.Equal("a", to.Name);
    Assert.Equal("n", to.Note);
  }

  [Fact]
  public void CopyToObject_honours_the_exclude_expression()
  {
    var from = new Plain { Id = 1, Name = "a", Note = "n" };
    var to = new Plain();

    from.CopyToObject(to, exclude: p => p.Id);

    Assert.Equal(0, to.Id);
    Assert.Equal("a", to.Name);
  }

  [Fact]
  public void CopyToObject_honours_a_multi_property_exclude_expression()
  {
    var from = new Plain { Id = 1, Name = "a", Note = "n" };
    var to = new Plain();

    from.CopyToObject(to, exclude: p => new { p.Id, p.Note });

    Assert.Equal(0, to.Id);
    Assert.Equal("a", to.Name);
    Assert.Equal("", to.Note);
  }

  [Fact]
  public void CopyToObject_honours_excludeProps()
  {
    var from = new Plain { Id = 1, Name = "a", Note = "n" };
    var to = new Plain();

    from.CopyToObject(to, excludeProps: [nameof(Plain.Note)]);

    Assert.Equal("a", to.Name);
    Assert.Equal("", to.Note);
  }

  [Fact]
  public void CopyFromObject_honours_excludeProps()
  {
    // CopyFromObject accepts excludeProps but never forwards it to CopyToObject.
    var from = new Plain { Id = 1, Name = "a", Note = "n" };
    var to = new Plain();

    to.CopyFromObject(from, excludeProps: [nameof(Plain.Note)]);

    Assert.Equal("a", to.Name);
    Assert.Equal("", to.Note);
  }

  [Fact]
  public void CopyToObject_skips_get_only_properties()
  {
    // Reflection copying calls SetValue unconditionally, so a computed property throws
    // ArgumentException: "Property set method not found."
    var from = new WithComputed { First = "Mario", Last = "Rossi" };
    var to = new WithComputed();

    var ex = Record.Exception(() => from.CopyToObject(to));

    Assert.Null(ex);
    Assert.Equal("Mario", to.First);
    Assert.Equal("Rossi", to.Last);
  }

  [Fact]
  public void CopyTo_clones_the_revision_rather_than_sharing_it()
  {
    var from = new Revisionable { Id = 1, Name = "a" };
    var to = new Revisionable();

    from.CopyTo(to);

    Assert.Equal("a", to.Name);
    Assert.NotSame(from.Revision, to.Revision);
    Assert.Equal(from.Revision.DocumentId, to.Revision.DocumentId);

    to.Revision.Deprecate();
    Assert.True(from.Revision.IsCurrent);
  }

  [Fact]
  public void CopyFrom_is_the_mirror_of_CopyTo()
  {
    var source = new Revisionable { Id = 7, Name = "source" };
    var target = new Revisionable();

    target.CopyFrom(source);

    Assert.Equal(7, target.Id);
    Assert.Equal("source", target.Name);
  }
}
