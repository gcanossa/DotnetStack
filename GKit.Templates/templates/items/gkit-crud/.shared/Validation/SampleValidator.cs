using FluentValidation;
using GKit.UI;

namespace GKIT-SHARED-NS;

/// <summary>
/// Shared verbatim between adapters: MudBlazor drives the rules through MudForm's per-field hook
/// and Radzen through an EditContext validator, but this is the same object either way.
/// Register it with <c>builder.Services.AddGKitValidator&lt;Sample, SampleValidator&gt;();</c>.
/// </summary>
public class SampleValidator : AbstractValidatorBase<Sample>
{
    public SampleValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer");

        RuleFor(p => p.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer");
    }
}
