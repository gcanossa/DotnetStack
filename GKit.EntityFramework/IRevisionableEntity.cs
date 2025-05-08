namespace GKit.EntityFramework;

public interface IRevisionableEntity : ICloneable
{
  public RevisionInfo Revision { get; set; }
}
