using GKit.Authentication.Blazor;
using Microsoft.AspNetCore.Components;
#if (auth_simple)
using GKit.App1.Data;
using GKit.Authentication.Simple;
#endif
#if (auth_ad)
using GKit.Authentication.ActiveDirectory;
#endif

namespace GKit.App1.Components.Account;

public abstract class LogoutBase : LogoutComponentBase
{
#if (auth_simple)
    [Inject] protected SimpleAccountManager<ApplicationDbContext, ApplicationUser> AccountManager { get; set; } = null!;
#endif
#if (auth_ad)
    [Inject] protected ADAccountManager AccountManager { get; set; } = null!;
#endif

    protected override Task SignOutAsync() => AccountManager.SignOutAsync();
}
