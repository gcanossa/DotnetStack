using Microsoft.AspNetCore.Components;

namespace GKit.UI.RadzenExt;

/// <summary>
/// Hosts <typeparamref name="TForm"/> in a Radzen dialog, validated by
/// <typeparamref name="TValidator"/> through <see cref="GKitFluentValidator{T}"/>.
/// </summary>
/// <remarks>
/// Subclasses supply only <see cref="EmptyValueFactory"/>. The only difference from the MudBlazor
/// adapter's version is the base type they name.
/// </remarks>
public abstract partial class EditEntityDialog<T, TForm, TValidator> : IEditEntityDialog<T>
  where T : class
  where TForm : IComponent, IEditEntityForm<T>
  where TValidator : AbstractValidatorBase<T>
{
  public abstract T EmptyValueFactory();
}
