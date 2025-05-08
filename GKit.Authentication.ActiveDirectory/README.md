# GKit.Authentication.ActiveDirectory

Allows to query an ActiveDirectory Domain Controller using LDAP, in order to authenticate a user and retrieve personale and RBAC information.

## Usage

Register the service in the DI:

```cs
services.AddActiveDirectoryAuthentication(adConfig: p =>
  {
    p.Host = "192.168.1.2";
    p.Domain = "win.virtualbox.org";
    p.QueryBase = "CN=Users, DC=win, DC=virtualbox, DC=org";
  });
```

Use the service:

```cs
var manager = scope.ServiceProvider.GetRequiredService<ADAccountManager>();

var result = manager.TryGetUserInfo("user", "user", out var user);

//...

await manager.SignInAsync("user", "password");
```
