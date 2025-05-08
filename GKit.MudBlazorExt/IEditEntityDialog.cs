namespace GKit.MudBlazorExt;

public interface IEditEntityDialog<T> where T : class
{
  public string Title { get; set; }
  public T Model { get; set; }

  public AbstractValidatorBase<T> Validator { get; }

  public T EmptyValueFactory();
}