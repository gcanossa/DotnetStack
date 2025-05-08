namespace GKit.Reporting;

public interface IReporter<T>
{
  public abstract Task WriteReportAsync(IEnumerable<T> data, Stream output);
}