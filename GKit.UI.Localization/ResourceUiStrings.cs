using System.Globalization;
using System.Resources;

namespace GKit.UI.Localization;

/// <summary>
/// Culture-aware <see cref="IGKitUiStrings"/> backed by embedded resources.
/// </summary>
/// <remarks>
/// <para>
/// Culture comes from <see cref="CultureInfo.CurrentUICulture"/>, so this responds to the host's
/// request localization without any additional wiring - the same mechanism Radzen uses for its own
/// strings, which keeps both layers on one culture setting.
/// </para>
/// <para>
/// Deriving from <see cref="DefaultUiStrings"/> means a key missing from the resources falls back
/// to the neutral English value rather than throwing or rendering a placeholder.
/// </para>
/// </remarks>
public class ResourceUiStrings : DefaultUiStrings
{
  private static readonly ResourceManager _resources =
    new("GKit.UI.Localization.GKitUiResources", typeof(ResourceUiStrings).Assembly);

  protected virtual string Get(string key, string fallback)
  {
    try
    {
      return _resources.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
    }
    catch (MissingManifestResourceException)
    {
      return fallback;
    }
  }

  public override string Edit => Get(nameof(Edit), base.Edit);
  public override string Delete => Get(nameof(Delete), base.Delete);
  public override string Add => Get(nameof(Add), base.Add);
  public override string Refresh => Get(nameof(Refresh), base.Refresh);
  public override string ExportXlsx => Get(nameof(ExportXlsx), base.ExportXlsx);
  public override string Save => Get(nameof(Save), base.Save);
  public override string Cancel => Get(nameof(Cancel), base.Cancel);
  public override string Ok => Get(nameof(Ok), base.Ok);
  public override string OpenRegistry => Get(nameof(OpenRegistry), base.OpenRegistry);
  public override string RowActionsMenu => Get(nameof(RowActionsMenu), base.RowActionsMenu);

  public override string CreateItemTitle => Get(nameof(CreateItemTitle), base.CreateItemTitle);
  public override string EditItemTitle => Get(nameof(EditItemTitle), base.EditItemTitle);
  public override string ConfirmTitle => Get(nameof(ConfirmTitle), base.ConfirmTitle);

  public override string ConfirmGeneric => Get(nameof(ConfirmGeneric), base.ConfirmGeneric);

  public override string ConfirmDelete(string itemDescription)
  {
    // The resource is a format string containing markup, so the interpolated entity text is
    // encoded here rather than relying on the base implementation.
    var format = Get(nameof(ConfirmDelete), "Delete <strong>{0}</strong>?");
    return string.Format(CultureInfo.CurrentCulture, format, HtmlEncode(itemDescription));
  }

  public override string ItemAdded => Get(nameof(ItemAdded), base.ItemAdded);
  public override string ItemAddFailed => Get(nameof(ItemAddFailed), base.ItemAddFailed);
  public override string ItemUpdated => Get(nameof(ItemUpdated), base.ItemUpdated);
  public override string ItemUpdateFailed => Get(nameof(ItemUpdateFailed), base.ItemUpdateFailed);
  public override string ItemDeleted => Get(nameof(ItemDeleted), base.ItemDeleted);
  public override string ItemDeleteFailed => Get(nameof(ItemDeleteFailed), base.ItemDeleteFailed);
  public override string FetchValuesFailed => Get(nameof(FetchValuesFailed), base.FetchValuesFailed);

  public override string NoRecords => Get(nameof(NoRecords), base.NoRecords);
  public override string NoResults => Get(nameof(NoResults), base.NoResults);

  public override string ColumnFallback(int index)
  {
    var format = Get(nameof(ColumnFallback), "Column {0}");
    return string.Format(CultureInfo.CurrentCulture, format, index);
  }
}
