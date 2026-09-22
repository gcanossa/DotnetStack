using Microsoft.EntityFrameworkCore;

namespace GKit.UI;

/// <summary>
/// A dialog that edits a single entity. Implementations are shared verbatim between adapters -
/// only the dialog shell that hosts them differs.
/// </summary>
public interface IEditEntityDialog<T> where T : class
{
  public string Title { get; set; }
  public T Model { get; set; }
  public DbContext Context { get; set; }

  public T EmptyValueFactory();
}

/// <summary>
/// The form body inside an edit dialog, with hooks around validation and submission.
/// </summary>
public interface IEditEntityForm<T> where T : class
{
  public T Model { get; set; }
  public DbContext Context { get; set; }

  public Task OnBeforeValidationAsync()
  {
    return Task.CompletedTask;
  }

  public Task OnAfterValidationAsync(bool validated)
  {
    return Task.CompletedTask;
  }

  public Task<bool> OnBeforeSubmitAsync()
  {
    return Task.FromResult(true);
  }
}

/// <summary>
/// The outcome of a caller-supplied factory that seeds a new entity before the create dialog
/// opens.
/// </summary>
public record NewValueResult<N>(bool Canceled, N Value);
