namespace GKit.UI;

/// <summary>
/// Every user-facing string GKit's own components render.
/// </summary>
/// <remarks>
/// Named members rather than string keys, so a missing translation is a compile error rather
/// than a runtime placeholder. The component libraries localise their own internals separately -
/// MudBlazor through MudBlazor.Translations, Radzen through its built-in satellite assemblies.
/// </remarks>
public interface IGKitUiStrings
{
  // Row and toolbar actions
  string Edit { get; }
  string Delete { get; }
  string Add { get; }
  string Refresh { get; }
  string ExportXlsx { get; }
  string Save { get; }
  string Cancel { get; }
  string Ok { get; }
  string OpenRegistry { get; }
  string RowActionsMenu { get; }

  // Dialog titles
  string CreateItemTitle { get; }
  string EditItemTitle { get; }
  string ConfirmTitle { get; }

  // Confirmations
  string ConfirmGeneric { get; }

  /// <summary>
  /// Confirmation prompt for deleting a named item. Implementations must HTML-encode
  /// <paramref name="itemDescription"/>: it is entity-derived and the result is rendered as markup.
  /// </summary>
  string ConfirmDelete(string itemDescription);

  // Outcomes
  string ItemAdded { get; }
  string ItemAddFailed { get; }
  string ItemUpdated { get; }
  string ItemUpdateFailed { get; }
  string ItemDeleted { get; }
  string ItemDeleteFailed { get; }
  string FetchValuesFailed { get; }

  // Empty states
  string NoRecords { get; }
  string NoResults { get; }

  // Export
  string ColumnFallback(int index);
}
