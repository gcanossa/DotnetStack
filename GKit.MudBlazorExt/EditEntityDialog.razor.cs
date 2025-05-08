using Microsoft.AspNetCore.Components;

namespace GKit.MudBlazorExt;

public abstract partial class EditEntityDialog<T, TForm> : IEditEntityDialog<T>
  where T : class
  where TForm : IComponent, IEditEntityForm<T>
{
  public EditEntityDialog()
  {
    _validator = ValidatorFactory();
  }

  protected AbstractValidatorBase<T> _validator = default!;
  public AbstractValidatorBase<T> Validator => _validator;

  protected abstract AbstractValidatorBase<T> ValidatorFactory();

  public abstract T EmptyValueFactory();
}