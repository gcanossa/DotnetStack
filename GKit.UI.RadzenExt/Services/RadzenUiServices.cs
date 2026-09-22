using Microsoft.AspNetCore.Components.Forms;
using Radzen;

namespace GKit.UI.RadzenExt;

/// <summary>
/// <see cref="IUiNotifier"/> over Radzen's notification service.
/// </summary>
public sealed class RadzenUiNotifier(NotificationService notifications) : IUiNotifier
{
  private void Notify(NotificationSeverity severity, string message) =>
    notifications.Notify(new NotificationMessage { Severity = severity, Summary = message });

  public void Success(string message) => Notify(NotificationSeverity.Success, message);
  public void Error(string message) => Notify(NotificationSeverity.Error, message);
  public void Warning(string message) => Notify(NotificationSeverity.Warning, message);
  public void Info(string message) => Notify(NotificationSeverity.Info, message);
}

/// <summary>
/// <see cref="IUiDialogs"/> over Radzen's dialog service.
/// </summary>
public sealed class RadzenUiDialogs(DialogService dialogs) : IUiDialogs
{
  public async Task<UiDialogResult> ShowAsync(
    Type component, string title, IReadOnlyDictionary<string, object?> parameters)
  {
    var radzenParameters = parameters.ToDictionary(p => p.Key, p => p.Value);

    var result = await dialogs.OpenAsync(title, component, radzenParameters);

    // Radzen signals dismissal by returning null rather than a cancelled flag.
    return result is null ? UiDialogResult.Cancelled : new UiDialogResult(false, result);
  }

  public async Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText)
  {
    var options = new ConfirmOptions { OkButtonText = okText, CancelButtonText = cancelText };

    return await dialogs.Confirm(message, title, options) ?? false;
  }
}

/// <summary>
/// <see cref="IUiDialogHost"/> over Radzen's dialog service.
/// </summary>
/// <remarks>
/// Unlike MudBlazor, Radzen closes dialogs through the service rather than a cascaded instance,
/// and has no separate cancel: closing with null is how dismissal is expressed.
/// </remarks>
public sealed class RadzenUiDialogHost(DialogService dialogs) : IUiDialogHost
{
  public void Close(object? result) => dialogs.Close(result);
  public void Cancel() => dialogs.Close(null);
}

/// <summary>
/// <see cref="IUiFormHandle"/> over an <see cref="EditContext"/>.
/// </summary>
/// <remarks>
/// Radzen has no per-field validation hook equivalent to MudForm's, so validation runs through
/// the EditContext and <see cref="GKitFluentValidator{T}"/>. "Touched" maps to
/// <see cref="EditContext.IsModified()"/>.
/// </remarks>
public sealed class RadzenUiFormHandle(EditContext context) : IUiFormHandle
{
  public bool IsTouched => context.IsModified();

  public bool IsValid => !context.GetValidationMessages().Any();

  public Task<bool> ValidateAsync() => Task.FromResult(context.Validate());

  public Task ResetAsync()
  {
    context.MarkAsUnmodified();
    return Task.CompletedTask;
  }
}

/// <summary>
/// Maps the shared icon set onto Radzen's Material Symbols ligature names.
/// </summary>
public sealed class RadzenUiIconSet : IUiIconSet
{
  public string Resolve(UiIcon icon) => icon switch
  {
    UiIcon.Add => "add",
    UiIcon.Edit => "edit",
    UiIcon.Delete => "delete",
    UiIcon.Download => "download",
    UiIcon.Refresh => "refresh",
    UiIcon.More => "more_vert",
    UiIcon.DragIndicator => "drag_indicator",
    UiIcon.Visibility => "visibility",
    UiIcon.VisibilityOff => "visibility_off",
    UiIcon.ContentCopy => "content_copy",
    UiIcon.Check => "check",
    UiIcon.Close => "close",
    UiIcon.Search => "search",
    _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unmapped icon")
  };
}

/// <summary>
/// Maps the shared colour set onto Radzen's button styles.
/// </summary>
public static class RadzenUiColorExtensions
{
  public static ButtonStyle ToButtonStyle(this UiColor color) => color switch
  {
    UiColor.Default => ButtonStyle.Base,
    UiColor.Primary => ButtonStyle.Primary,
    UiColor.Secondary => ButtonStyle.Secondary,
    UiColor.Info => ButtonStyle.Info,
    UiColor.Success => ButtonStyle.Success,
    UiColor.Warning => ButtonStyle.Warning,
    UiColor.Error => ButtonStyle.Danger,
    _ => ButtonStyle.Base
  };
}
