using Microsoft.Extensions.Localization;
using MudBlazor;

namespace GC.MudBlazor.Localization;

internal class DictionaryMudLocalizer(IEnumerable<MudBlazorLocalizationBase> localizations) : MudLocalizer
{
  private readonly IEnumerable<MudBlazorLocalizationBase> _localizations = localizations;

  public override LocalizedString this[string key]
  {
    get
    {
      var currentCulture = Thread.CurrentThread.CurrentUICulture.Parent.TwoLetterISOLanguageName;

      var translation = _localizations.FirstOrDefault(p => p.CultureCode.Equals(currentCulture, StringComparison.CurrentCultureIgnoreCase));

      if (translation is not null
          && translation.TryGetValue(key, out var res))
      {
        return new(key, res!);
      }
      else
      {
        return new(key, key, true);
      }
    }
  }
}
