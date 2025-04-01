using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace GC.MudBlazor.Localization;

public interface IMudBlazorLocalizationConfiguration
{

}

internal class MudBlazorLocalizationConfigurationImpl(IServiceCollection services) : IMudBlazorLocalizationConfiguration
{
  public readonly IServiceCollection Services = services;
}

public static class LocalizationExtensions
{
  public static IMudBlazorLocalizationConfiguration AddMudBlazorLocalization(this IServiceCollection ext, CultureInfo defaultCulture)
  {
    CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
    CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

    ext.AddTransient<MudLocalizer, DictionaryMudLocalizer>();

    return new MudBlazorLocalizationConfigurationImpl(ext);
  }

  public static IMudBlazorLocalizationConfiguration AddLanguage<T>(this IMudBlazorLocalizationConfiguration ext)
    where T : MudBlazorLocalizationBase
  {
    var services = ((MudBlazorLocalizationConfigurationImpl)ext).Services;

    services.AddTransient<MudBlazorLocalizationBase, T>();

    return ext;
  }
}