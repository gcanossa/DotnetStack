using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GKit.UI.MudBlazorExt;

/// <summary>
/// <see cref="IUiNotifier"/> over MudBlazor's snackbar.
/// </summary>
public sealed class MudUiNotifier(ISnackbar snackbar) : IUiNotifier
{
  public void Success(string message) => snackbar.Add(message, Severity.Success);
  public void Error(string message) => snackbar.Add(message, Severity.Error);
  public void Warning(string message) => snackbar.Add(message, Severity.Warning);
  public void Info(string message) => snackbar.Add(message, Severity.Info);
}

/// <summary>
/// <see cref="IUiDialogs"/> over MudBlazor's dialog service.
/// </summary>
public sealed class MudUiDialogs(IDialogService dialogs) : IUiDialogs
{
  public async Task<UiDialogResult> ShowAsync(
    Type component, string title, IReadOnlyDictionary<string, object?> parameters)
  {
    var dialogParameters = new DialogParameters();
    foreach (var (key, value) in parameters)
      dialogParameters.Add(key, value);

    var reference = await dialogs.ShowAsync(component, title, dialogParameters);
    var result = await reference.Result;

    // A null result means the dialog was dismissed rather than closed with a value.
    return result is null || result.Canceled
      ? UiDialogResult.Cancelled
      : new UiDialogResult(false, result.Data);
  }

  public async Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText)
  {
    return await dialogs.ShowMessageBoxAsync(
      title, (MarkupString)message, yesText: okText, noText: cancelText) ?? false;
  }
}

/// <summary>
/// <see cref="IUiDialogHost"/> over a MudBlazor dialog instance.
/// </summary>
public sealed class MudUiDialogHost(IMudDialogInstance instance) : IUiDialogHost
{
  public void Close(object? result) => instance.Close(DialogResult.Ok(result));
  public void Cancel() => instance.Cancel();
}

/// <summary>
/// <see cref="IUiFormHandle"/> over a MudForm.
/// </summary>
/// <remarks>
/// MudForm is kept as the MudBlazor form model rather than moving to EditContext, so the
/// touched-gated save button and validation timing behave exactly as they did before the
/// adapter split.
/// </remarks>
public sealed class MudUiFormHandle(MudForm form) : IUiFormHandle
{
  public bool IsTouched => form.IsTouched;
  public bool IsValid => form.IsValid;

  public async Task<bool> ValidateAsync()
  {
    await form.ValidateAsync();
    return form.IsValid;
  }

  public Task ResetAsync() => form.ResetValidationAsync();
}

/// <summary>
/// Maps the shared icon set onto MudBlazor's Material SVG paths.
/// </summary>
public sealed class MudUiIconSet : IUiIconSet
{
  public string Resolve(UiIcon icon) => icon switch
  {
    UiIcon.Add => Icons.Material.Filled.Add,
    UiIcon.Edit => Icons.Material.Filled.Edit,
    UiIcon.Delete => Icons.Material.Filled.Delete,
    UiIcon.Download => Icons.Material.Filled.Download,
    UiIcon.Refresh => Icons.Material.Filled.Refresh,
    UiIcon.More => Icons.Material.Filled.MoreVert,
    UiIcon.DragIndicator => Icons.Material.Filled.DragIndicator,
    UiIcon.Visibility => Icons.Material.Filled.Visibility,
    UiIcon.VisibilityOff => Icons.Material.Filled.VisibilityOff,
    UiIcon.ContentCopy => Icons.Material.Filled.ContentCopy,
    UiIcon.Check => Icons.Material.Filled.Check,
    UiIcon.Close => Icons.Material.Filled.Close,
    UiIcon.Search => Icons.Material.Filled.Search,
    _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unmapped icon")
  };
}

/// <summary>
/// Maps the shared colour set onto MudBlazor's.
/// </summary>
public static class MudUiColorExtensions
{
  public static Color ToMudColor(this UiColor color) => color switch
  {
    UiColor.Default => Color.Default,
    UiColor.Primary => Color.Primary,
    UiColor.Secondary => Color.Secondary,
    UiColor.Info => Color.Info,
    UiColor.Success => Color.Success,
    UiColor.Warning => Color.Warning,
    UiColor.Error => Color.Error,
    _ => Color.Default
  };
}
