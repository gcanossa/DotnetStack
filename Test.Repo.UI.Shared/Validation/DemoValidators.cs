using FluentValidation;
using GKit.UI;

namespace Test.Repo.UI.Shared;

/// <summary>
/// Validators are shared verbatim between adapters: MudBlazor drives them through MudForm's
/// per-field hook, Radzen through an EditContext validator, but the rules are the same object.
/// </summary>
public class WidgetValidator : AbstractValidatorBase<Widget>
{
  public WidgetValidator()
  {
    RuleFor(w => w.Name)
      .NotEmpty().WithMessage("Name is required")
      .MaximumLength(50).WithMessage("Name must be 50 characters or fewer");

    RuleFor(w => w.Quantity)
      .InclusiveBetween(0, 1000).WithMessage("Quantity must be between 0 and 1000");

    RuleFor(w => w.Notes)
      .MaximumLength(200).WithMessage("Notes must be 200 characters or fewer");
  }
}

public class CategoryValidator : AbstractValidatorBase<Category>
{
  public CategoryValidator()
  {
    RuleFor(c => c.Name)
      .NotEmpty().WithMessage("Name is required")
      .MaximumLength(30).WithMessage("Name must be 30 characters or fewer");
  }
}
