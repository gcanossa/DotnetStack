using System.Net;

namespace GKit.UI;

/// <summary>
/// The neutral (English) strings, used when <c>GKit.UI.Localization</c> is not referenced.
/// </summary>
/// <remarks>
/// English is the neutral fallback by .NET convention. Italian ships as an opt-in resource in
/// <c>GKit.UI.Localization</c>; referencing that package and running under <c>it-IT</c> restores
/// the wording these components used before the strings were extracted.
/// </remarks>
public class DefaultUiStrings : IGKitUiStrings
{
  public virtual string Edit => "Edit";
  public virtual string Delete => "Delete";
  public virtual string Add => "Add";
  public virtual string Refresh => "Refresh";
  public virtual string ExportXlsx => "Export XLSX";
  public virtual string Save => "Save";
  public virtual string Cancel => "Cancel";
  public virtual string Ok => "OK";
  public virtual string OpenRegistry => "Open registry";
  public virtual string RowActionsMenu => "Row actions";

  public virtual string CreateItemTitle => "Create item";
  public virtual string EditItemTitle => "Edit item";
  public virtual string ConfirmTitle => "Confirm operation";

  public virtual string ConfirmGeneric => "Confirm this operation?";

  public virtual string ConfirmDelete(string itemDescription) =>
    $"Delete <strong>{HtmlEncode(itemDescription)}</strong>?";

  public virtual string ItemAdded => "Item added";
  public virtual string ItemAddFailed => "Could not add the item";
  public virtual string ItemUpdated => "Item updated";
  public virtual string ItemUpdateFailed => "Could not update the item";
  public virtual string ItemDeleted => "Item deleted";
  public virtual string ItemDeleteFailed => "Could not delete the item";
  public virtual string FetchValuesFailed => "Could not load values";

  public virtual string NoRecords => "No records";
  public virtual string NoResults => "No matches found";

  public virtual string ColumnFallback(int index) => $"Column {index}";

  /// <summary>
  /// Encodes text that will be interpolated into a string rendered as markup.
  /// </summary>
  /// <remarks>
  /// The delete confirmation embeds entity-derived text in markup. Before the strings were
  /// extracted this interpolation was unencoded, which made any user-controlled entity field a
  /// stored-XSS sink.
  /// </remarks>
  protected static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
