namespace GKit.UI;

/// <summary>
/// Transient user feedback - MudBlazor's snackbar, Radzen's notifications.
/// </summary>
public interface IUiNotifier
{
  void Success(string message);
  void Error(string message);
  void Warning(string message);
  void Info(string message);
}

/// <summary>
/// The outcome of a dialog.
/// </summary>
/// <remarks>
/// The two libraries disagree on how dismissal is reported: MudBlazor returns an explicit
/// cancelled flag, Radzen returns null. Adapters normalise both onto this.
/// </remarks>
public sealed record UiDialogResult(bool Canceled, object? Data)
{
  public static UiDialogResult Cancelled { get; } = new(true, null);
}

/// <summary>
/// Opening dialogs and asking for confirmation.
/// </summary>
public interface IUiDialogs
{
  Task<UiDialogResult> ShowAsync(Type component, string title, IReadOnlyDictionary<string, object?> parameters);

  Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText);
}

/// <summary>
/// The handle a dialog uses to close itself, cascaded in by whichever adapter opened it.
/// </summary>
public interface IUiDialogHost
{
  void Close(object? result);
  void Cancel();
}

/// <summary>
/// A form's validation state, as seen by the shared dialog engine.
/// </summary>
/// <remarks>
/// MudBlazor backs this with MudForm and Radzen with EditContext; the two have genuinely
/// different validation models, and this interface is the whole of what the shared code needs
/// from either.
/// </remarks>
public interface IUiFormHandle
{
  bool IsTouched { get; }
  bool IsValid { get; }

  Task<bool> ValidateAsync();
  Task ResetAsync();
}

/// <summary>
/// Imperative control over a lookup/autocomplete component, needed when creating an entity
/// inline has to close the dropdown and select the new value.
/// </summary>
public interface IUiLookupHandle<in T>
{
  Task CloseDropDownAsync();
  Task SelectAsync(T value);
}
