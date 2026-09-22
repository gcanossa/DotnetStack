# GKit.Authentication.Simple

Signs users in against an account entity owned by the application and stored through EntityFramework,
using cookie authentication. It is the local-accounts counterpart of **GKit.Authentication.ActiveDirectory**
and exposes the same shape (`TryGetUserInfo`, `SignInAsync`, `SignOutAsync`), so switching an application
from one to the other is mostly a matter of changing the registration.

## Usage

Make the account entity implement `ISimpleAccountUser`:

```cs
public class ApplicationUser : ISimpleAccountUser, ISoftDeletableEntity
{
  public int Id { get; set; }
  public required string AccountName { get; set; }
  public string? PasswordHash { get; set; }
  public string? Mail { get; set; }
  public bool IsManager { get; set; }
  public DateTime? DeletedAt { get; set; }

  public string Identifier => Id.ToString();
}
```

Register the services in the DI, after the `DbContext` factory:

```cs
builder.Services.AddDbContextFactory<ApplicationDbContext>(/* ... */);

builder.Services.AddSimpleAuthentication<ApplicationDbContext, ApplicationUser>(
  cookieConfig: options =>
  {
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
  },
  simpleConfig: options =>
  {
    // only non deleted managers may sign in
    options.SignInQuery = q => q.IgnoreQueryFilters().WithoutDeleted().Where(p => p.IsManager);
  });
```

Options bind from the `GKit:SimpleAuthentication` configuration section first, then the
`simpleConfig` delegate is applied on top, so code wins over `appsettings.json`. Only
`AuthenticationType` is bindable; the query and claim hooks are delegates and must be set in code.

Use the service:

```cs
var manager = scope.ServiceProvider
  .GetRequiredService<SimpleAccountManager<ApplicationDbContext, ApplicationUser>>();

await manager.RegisterAsync(new ApplicationUser { AccountName = "user", IsManager = true }, "password");

await manager.SignInAsync("user", "password");
```

The closed generic is verbose in Razor files; derive a thin alias in the application:

```cs
public class AppAccountManager(
  IHttpContextAccessor httpContextAccessor,
  IDbContextFactory<ApplicationDbContext> dbContextFactory,
  IPasswordHasher passwordHasher,
  IOptions<SimpleAccountManagerOptions<ApplicationUser>> options,
  ILogger<SimpleAccountManager<ApplicationDbContext, ApplicationUser>> logger)
  : SimpleAccountManager<ApplicationDbContext, ApplicationUser>(
    httpContextAccessor, dbContextFactory, passwordHasher, options, logger);
```

## Login and logout pages

The `LoginComponentBase<T>` and `LogoutComponentBase` types of **GKit.Authentication.Blazor** are
the intended front end:

```razor
@page "/account/login"
@inherits GKit.Authentication.Blazor.LoginComponentBase<Credentials>
@inject SimpleAccountManager<ApplicationDbContext, ApplicationUser> SignInManager

@* ... form bound to Model ... *@

@code {
  protected override async Task<bool> SignInAsync(Credentials credentials)
    => await SignInManager.SignInAsync(credentials.Username, credentials.Password);
}
```

## Password hashing

`IPasswordHasher` decides the format of the stored `PasswordHash`.

| Implementation | Format | Use |
| --- | --- | --- |
| `Pbkdf2PasswordHasher` (default) | `pbkdf2$sha256$<iterations>$<salt>$<hash>` | new applications |
| `Sha512PasswordHasher` | lowercase hex of a single unsalted SHA512 round | existing databases only |

`AddSimpleAuthentication` registers PBKDF2 unless an `IPasswordHasher` is already registered, so an
application on the legacy format opts in explicitly:

```cs
builder.Services.AddSingleton<IPasswordHasher, Sha512PasswordHasher>();
builder.Services.AddSimpleAuthentication<ApplicationDbContext, ApplicationUser>();
```

Migrating an existing database means rehashing every account: keep `Sha512PasswordHasher` until each
password has been re-set through `ResetPasswordAsync` under the new hasher.
