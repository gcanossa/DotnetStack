namespace GC.MudBlazor.Localization;

public abstract class MudBlazorLocalizationBase
{
  public abstract string CultureCode { get; }

  private readonly Dictionary<string, string> Translations = [];

  protected readonly IEnumerable<string> TranslationKeys = [
    "MudDataGrid.!=",
    "MudDataGrid.<",
    "MudDataGrid.<=",
    "MudDataGrid.=",
    "MudDataGrid.>",
    "MudDataGrid.>=",
    "MudDataGrid.AddFilter",
    "MudDataGrid.Apply",
    "MudDataGrid.Cancel",
    "MudDataGrid.Clear",
    "MudDataGrid.CollapseAllGroups",
    "MudDataGrid.Column",
    "MudDataGrid.Columns",
    "MudDataGrid.contains",
    "MudDataGrid.ends with",
    "MudDataGrid.equals",
    "MudDataGrid.ExpandAllGroups",
    "MudDataGrid.False",
    "MudDataGrid.Filter",
    "MudDataGrid.FilterValue",
    "MudDataGrid.Group",
    "MudDataGrid.Hide",
    "MudDataGrid.HideAll",
    "MudDataGrid.is",
    "MudDataGrid.is after",
    "MudDataGrid.is before",
    "MudDataGrid.is empty",
    "MudDataGrid.is not",
    "MudDataGrid.is not empty",
    "MudDataGrid.is on or after",
    "MudDataGrid.is on or before",
    "MudDataGrid.MoveDown",
    "MudDataGrid.MoveUp",
    "MudDataGrid.not contains",
    "MudDataGrid.not equals",
    "MudDataGrid.Operator",
    "MudDataGrid.RefreshData",
    "MudDataGrid.Save",
    "MudDataGrid.ShowAll",
    "MudDataGrid.Sort",
    "MudDataGrid.starts with",
    "MudDataGrid.True",
    "MudDataGrid.Ungroup",
    "MudDataGrid.Unsort",
    "MudDataGrid.Value"
  ];

  public MudBlazorLocalizationBase()
  {
    PrepareTranslations(Translations);
    ValidateTranslations(Translations);
  }

  protected abstract void PrepareTranslations(Dictionary<string, string> translations);

  protected void ValidateTranslations(Dictionary<string, string> translations)
  {
    var missing = TranslationKeys.Where(p => !translations.ContainsKey(p) || translations[p] is null).ToList();
    if (missing.Count > 0)
    {
      throw new Exception($"Some translation keys are missing: {string.Join(",", missing)}");
    }
  }

  public bool TryGetValue(string key, out string? value)
  {
    return Translations.TryGetValue(key, out value);
  }
}