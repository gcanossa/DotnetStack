using System.Text.Json;
using GKit.EntityFramework;
using GKit.Reporting;

namespace Test.Repo.EntityFramework;

public class RevisionExtensionsTest
{
  [Fact]
  public async Task CloneEntity()
  {
    var now = DateTime.Now;

    TestObject obj = new TestObject
    {
      Num = 10,
      DateNow = now,
      Title = "Test"
    };

    TestObject val = new TestObject();

    obj.CopyTo(val, p => new { p.Title });

    Assert.Equal(obj.Num, val.Num);
    Assert.Equal(obj.DateNow, val.DateNow);
    Assert.NotEqual(obj.Title, val.Title);
    Assert.Equal(JsonSerializer.Serialize(obj.Revision), JsonSerializer.Serialize(val.Revision));

    obj.CopyTo(val, p => p.Title);

    Assert.Equal(obj.Num, val.Num);
    Assert.Equal(obj.DateNow, val.DateNow);
    Assert.NotEqual(obj.Title, val.Title);
    Assert.Equal(JsonSerializer.Serialize(obj.Revision), JsonSerializer.Serialize(val.Revision));
  }

  private class TestObject : IRevisionableEntity
  {
    public int Num { get; set; }
    public DateTime DateNow { get; set; }
    public string Title { get; set; } = "";
    public RevisionInfo Revision { get; set; } = RevisionInfo.First();

    public object Clone()
    {
      throw new NotImplementedException();
    }
  }
}