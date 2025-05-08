# GKit.Authenitcation.Blazor

In order to use the component you should register a **CascadingValue**:

```cs
builder.Services.AddCascadingValue(sp => new AuthenticationOptions {
  LoginPath = "/login",
  AccessDeniedPath = "/logout"
});
```

And user the correct components to cause redirects:

```razor
<RedirectToAccessDenied />

...

<RedirectToLogin />
```

In order to ease the development of the **LoginPage** and the **LogOutPage** components you can derive the **LoginComponentBase** and **LogoutComponentBase** abstract classes.
