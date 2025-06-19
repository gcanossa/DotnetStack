using FluentValidation;

namespace GKit.MudBlazorExt;

public abstract class AbstractValidatorBase<T> : AbstractValidator<T>
{
  public async Task<IEnumerable<string>> ValidateValueAsync(object model, string propertyName)
  {
    var result = await ValidateAsync(ValidationContext<T>.CreateWithOptions((T)model, x => x.IncludeProperties(propertyName)));
    if (result.IsValid)
      return [];
    return result.Errors.Select(e => e.ErrorMessage);
  }
}