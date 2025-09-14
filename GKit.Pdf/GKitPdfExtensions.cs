using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;

namespace GKit.Pdf;

public static class GKitPdfExtensions
{
    public static IServiceCollection AddGKitPdfServices(this IServiceCollection ext, Action<GKitPdfOptions> configure = null)
    {
        var options = new GKitPdfOptions();
        configure?.Invoke(options);

        if(
            Environment.OSVersion.Platform == PlatformID.Win32NT || 
            Environment.OSVersion.Platform == PlatformID.Win32S || 
            Environment.OSVersion.Platform == PlatformID.Win32Windows)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
        else
        {
            throw new NotSupportedException("No font resolver available for the current platform");
        }
        
        return ext;
    }
}