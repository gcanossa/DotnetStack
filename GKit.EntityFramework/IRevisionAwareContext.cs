namespace GKit.EntityFramework;

public interface IRevisionAwareContext
{
  public RevisionInterceptor RevisionInterceptor { get; }
}
