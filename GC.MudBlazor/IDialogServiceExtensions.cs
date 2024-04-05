using MudBlazor;

namespace GC.MudBlazor;

public static class IDialogServiceExtensions
{
  public static async Task<DialogResult> Confirm(this IDialogService service, string title, string contentText, Color? color = null, string? okText = null, string? cancelText = null)
  {
    var parameters = new DialogParameters
    {
      { nameof(ConfirmationDialog.ContentText), contentText }
    };
    if (okText is not null) parameters.Add(nameof(ConfirmationDialog.OkButtonText), okText);
    if (cancelText is not null) parameters.Add(nameof(ConfirmationDialog.CancelButtonText), cancelText);
    if (color is not null) parameters.Add(nameof(ConfirmationDialog.Color), color);

    var options = new DialogOptions() { CloseButton = false, MaxWidth = MaxWidth.ExtraSmall };

    var dialog = await service.ShowAsync<ConfirmationDialog>(title, parameters, options);
    return await dialog.Result;
  }
}