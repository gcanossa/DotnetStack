using System.Globalization;

namespace Test.Repo.UI;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentUICulture"/> for the duration of a test and restores it
/// afterwards.
/// </summary>
public sealed class CultureScope : IDisposable
{
  private readonly CultureInfo _previousUi;
  private readonly CultureInfo _previous;

  public CultureScope(string culture)
  {
    _previousUi = CultureInfo.CurrentUICulture;
    _previous = CultureInfo.CurrentCulture;

    var target = new CultureInfo(culture);
    CultureInfo.CurrentUICulture = target;
    CultureInfo.CurrentCulture = target;
  }

  public void Dispose()
  {
    CultureInfo.CurrentUICulture = _previousUi;
    CultureInfo.CurrentCulture = _previous;
  }
}
