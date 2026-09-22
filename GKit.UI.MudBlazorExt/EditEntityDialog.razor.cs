using Microsoft.AspNetCore.Components;

namespace GKit.UI.MudBlazorExt;

/// <summary>
/// Hosts <typeparamref name="TForm"/> in a MudBlazor dialog, validated by
/// <typeparamref name="TValidator"/>.
/// </summary>
/// <remarks>
/// Subclasses supply only <see cref="EmptyValueFactory"/> and carry no MudBlazor types, so the
/// same subclass compiles against the Radzen adapter unchanged.
/// </remarks>
public abstract partial class EditEntityDialog<T, TForm, TValidator> : IEditEntityDialog<T>
  where T : class
  where TForm : IComponent, IEditEntityForm<T>
  where TValidator : AbstractValidatorBase<T>
{
  public abstract T EmptyValueFactory();
}
