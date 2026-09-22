using System.Globalization;

namespace GKit.Tests.Infrastructure;

/// <summary>
/// Temporarily switches the ambient culture. These libraries are deployed on it-IT machines,
/// where the decimal separator is ',' — several persistence paths format with CurrentCulture.
/// </summary>
public sealed class CultureScope : IDisposable
{
  private readonly CultureInfo _culture;
  private readonly CultureInfo _uiCulture;

  public CultureScope(string name)
  {
    _culture = CultureInfo.CurrentCulture;
    _uiCulture = CultureInfo.CurrentUICulture;

    var target = CultureInfo.GetCultureInfo(name);
    CultureInfo.CurrentCulture = target;
    CultureInfo.CurrentUICulture = target;
  }

  public void Dispose()
  {
    CultureInfo.CurrentCulture = _culture;
    CultureInfo.CurrentUICulture = _uiCulture;
  }
}
