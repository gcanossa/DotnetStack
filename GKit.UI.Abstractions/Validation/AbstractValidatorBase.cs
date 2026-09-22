using FluentValidation;

namespace GKit.UI;

/// <summary>
/// Base for FluentValidation validators used by GKit edit forms.
/// </summary>
/// <remarks>
/// <see cref="ValidateValueAsync"/> matches the per-field validation signature MudBlazor's
/// MudForm expects; the Radzen adapter drives the same validator through an EditContext
/// instead. Subclasses are therefore shared unchanged between the two adapters.
/// </remarks>
public abstract class AbstractValidatorBase<T> : AbstractValidator<T>
{
  public async Task<IEnumerable<string>> ValidateValueAsync(object model, string propertyName)
  {
    var result = await ValidateAsync(
      ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));

    if (result.IsValid)
      return [];

    return result.Errors.Select(e => e.ErrorMessage);
  }
}
