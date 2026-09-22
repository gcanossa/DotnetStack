using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace GKit.UI.RadzenExt;

/// <summary>
/// Drives an <see cref="AbstractValidatorBase{T}"/> from an <see cref="EditContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// MudBlazor accepts a per-field validation delegate directly on its form; Radzen has no
/// equivalent, so this component bridges the same validator onto the EditContext model. That is
/// what lets validator classes be shared verbatim between the two adapters.
/// </para>
/// <para>
/// Place it inside a <c>RadzenTemplateForm</c>. It validates the whole model on submit and the
/// single changed field on edit, so errors appear as the user moves through the form rather than
/// only at the end.
/// </para>
/// </remarks>
public class GKitFluentValidator<T> : ComponentBase, IDisposable
  where T : class
{
  private EditContext? _subscribed;
  private ValidationMessageStore? _messages;

  [CascadingParameter] private EditContext CurrentEditContext { get; set; } = default!;

  [Inject] private IValidator<T> Validator { get; set; } = default!;

  protected override void OnParametersSet()
  {
    if (CurrentEditContext is null)
      throw new InvalidOperationException(
        $"{nameof(GKitFluentValidator<T>)} requires a cascading {nameof(EditContext)}; " +
        "place it inside a RadzenTemplateForm or EditForm.");

    if (ReferenceEquals(_subscribed, CurrentEditContext))
      return;

    Detach();

    _subscribed = CurrentEditContext;
    _messages = new ValidationMessageStore(_subscribed);

    _subscribed.OnValidationRequested += OnValidationRequested;
    _subscribed.OnFieldChanged += OnFieldChanged;
  }

  private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
  {
    if (_subscribed?.Model is not T model)
      return;

    _messages!.Clear();

    var result = Validator.Validate(new ValidationContext<T>(model));

    foreach (var failure in result.Errors)
      _messages.Add(_subscribed.Field(failure.PropertyName), failure.ErrorMessage);

    _subscribed.NotifyValidationStateChanged();
  }

  private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
  {
    if (_subscribed?.Model is not T model)
      return;

    var propertyName = e.FieldIdentifier.FieldName;

    _messages!.Clear(e.FieldIdentifier);

    var result = Validator.Validate(
      ValidationContext<T>.CreateWithOptions(model, options => options.IncludeProperties(propertyName)));

    foreach (var failure in result.Errors.Where(f => f.PropertyName == propertyName))
      _messages.Add(e.FieldIdentifier, failure.ErrorMessage);

    _subscribed.NotifyValidationStateChanged();
  }

  private void Detach()
  {
    if (_subscribed is null)
      return;

    _subscribed.OnValidationRequested -= OnValidationRequested;
    _subscribed.OnFieldChanged -= OnFieldChanged;
    _messages?.Clear();
    _subscribed = null;
    _messages = null;
  }

  public void Dispose()
  {
    Detach();
    GC.SuppressFinalize(this);
  }
}
