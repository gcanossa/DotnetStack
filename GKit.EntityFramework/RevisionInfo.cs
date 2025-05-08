using Visus.Cuid;

namespace GKit.EntityFramework;

public class RevisionInfo
{
  public bool IsCurrent { get; set; }
  public DateTime CreatedAt { get; set; }
  public int Revision { get; set; }
  public string DocumentId { get; set; } = new Cuid2().ToString();

  public void Deprecate()
  {
    IsCurrent = false;
  }

  public RevisionInfo NewRevision(bool current = true)
  {
    return new RevisionInfo
    {
      IsCurrent = current,
      CreatedAt = DateTime.Now,
      Revision = Revision + 1,
      DocumentId = DocumentId
    };
  }
}
