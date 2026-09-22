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

/// <summary>
/// Holds the part of the login page that depends on the authentication mode, so Login.razor stays
/// markup only and identical under every UI adapter.
/// </summary>
public abstract class LoginBase : LoginComponentBase<Credentials>
{
#if (auth_simple)
    [Inject] protected SimpleAccountManager<ApplicationDbContext, ApplicationUser> AccountManager { get; set; } = null!;
#endif
#if (auth_ad)
    [Inject] protected ADAccountManager AccountManager { get; set; } = null!;
#endif

    protected override Task<bool> SignInAsync(Credentials credentials) =>
        AccountManager.SignInAsync(credentials.Username, credentials.Password);
}
