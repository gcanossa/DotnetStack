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
    var rev = Clone();
    rev.Revision++;
    rev.CreatedAt = DateTime.Now;
    rev.IsCurrent = current;

    return rev;
  }

  public RevisionInfo Clone()
  {
    return new RevisionInfo
    {
      IsCurrent = IsCurrent,
      CreatedAt = CreatedAt,
      Revision = Revision,
      DocumentId = DocumentId
    };
  }

  public static RevisionInfo First()
  {
    return new RevisionInfo
    {
      IsCurrent = true,
      CreatedAt = DateTime.Now,
      Revision = 0
    };
  }
}
