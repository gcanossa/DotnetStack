# GKit.UI.Localization

Culture-aware strings for the GKit UI components. Optional: without it the components render the
neutral English defaults from `GKit.UI.Abstractions`.

Ships **English** (neutral) and **Italian**.

## Usage

```cs
builder.Services.AddGKitUiCore();
builder.Services.AddGKitUiLocalization();
builder.Services.AddGKitMudBlazorUi();   // or AddGKitRadzenUi()
```

Culture comes from `CultureInfo.CurrentUICulture`, so standard request localization is all that is
needed:

```cs
app.UseRequestLocalization(new RequestLocalizationOptions()
  .SetDefaultCulture("it-IT")
  .AddSupportedUICultures("en", "it"));
```

`AddGKitUiLocalization()` and `AddGKitUiCore()` may be called in either order.

## Scope

This package covers **GKit's own strings** - grid toolbars, row actions, dialog titles,
confirmations, outcome notifications, empty states. The component libraries localize their own
internals separately:

| Library | Its own strings |
|---|---|
| MudBlazor | needs the separate `MudBlazor.Translations` package |
| Radzen | built in - ships `de`, `es`, `fr`, `it`, `ja` satellite assemblies, no extra package |

Both respond to the same `CurrentUICulture`, so one culture setting drives everything.

## Italian is opt-in

GKit's components were originally written with Italian strings hard-coded. They are now English by
default, per .NET's neutral-resource convention. An app that wants the original wording must
reference this package **and** run under `it-IT`.

## Overriding individual strings

`ResourceUiStrings` derives from `DefaultUiStrings` and every member is virtual, so overriding a
handful of strings needs no resource files:

```cs
public sealed class MyStrings : ResourceUiStrings
{
  public override string OpenRegistry => "Apri scheda cliente";
}

services.Replace(ServiceDescriptor.Scoped<IGKitUiStrings, MyStrings>());
```

Anything not found in the resources falls back to the neutral English value rather than throwing.
