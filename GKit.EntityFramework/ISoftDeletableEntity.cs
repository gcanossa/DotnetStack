namespace GKit.EntityFramework;

public interface ISoftDeletableEntity
{
  public DateTime? DeletedAt { get; set; }
}
