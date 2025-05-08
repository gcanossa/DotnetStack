namespace GKit.MudBlazorExt;

public interface IEditEntityForm<T> where T : class
{
  public T Model { get; set; }
}