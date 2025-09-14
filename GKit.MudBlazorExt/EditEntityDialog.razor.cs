using FluentValidation;
using Microsoft.AspNetCore.Components;

namespace GKit.MudBlazorExt;

public abstract partial class EditEntityDialog<T, TForm, TValidator> : IEditEntityDialog<T>
  where T : class
  where TForm : IComponent, IEditEntityForm<T>
  where TValidator : AbstractValidatorBase<T>
{
  public abstract T EmptyValueFactory();
}