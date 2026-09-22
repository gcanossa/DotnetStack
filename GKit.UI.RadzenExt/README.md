# GKit.UI.RadzenExt

Radzen adapter for the GKit UI components — the sibling of `GKit.UI.MudBlazorExt` over the same
`GKit.UI.Data` engine.

An application picks **one** adapter. Component names, type parameters and parameter names match
the MudBlazor adapter, so moving a page between them is a package swap, an `@using` change, and a
rewrite of the column markup and form bodies. Everything below that line — validators, dialog
subclasses, query factories, row-control descriptors, export, CRUD semantics — is unchanged.

## Setup

```cs
builder.Services.AddRadzenComponents();     // Radzen's own setup
builder.Services.AddGKitBlazorServices();   // GKit.BlazorExt
builder.Services.AddGKitRadzenUi();         // this package (calls AddGKitUiCore internally)
builder.Services.AddGKitUiLocalization();   // optional, for Italian

// Validators must be resolvable as IValidator<T> for the EditContext bridge:
builder.Services.AddGKitValidator<Widget, WidgetValidator>();
```

```razor
@* MainLayout.razor *@
<RadzenComponents />
```

```razor
@* _Imports.razor *@
@using Radzen
@using Radzen.Blazor
@using GKit.BlazorExt
@using GKit.UI
@using GKit.UI.Data
@using GKit.UI.RadzenExt
```

Radzen icons are Material Symbols ligatures, so the font must be linked in `App.razor`:

```html
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined" rel="stylesheet" />
```

## Components

Same set and same names as the MudBlazor adapter: `ManagedGrid<T>`, `EntityGrid<T, TDialog>`,
`EditEntityDialog<T, TForm, TValidator>`, `EntityAutocomplete<T>`,
`EntityItemAutocomplete<T, TItem>`, `ManagedAutocomplete<T>`,
`ManagedItemAutocomplete<T, TItem>`, `DataAutocomplete<T>`, `ManagedText`, `TimeRangePicker`.

## Differences from the MudBlazor adapter

These are the places where the two genuinely diverge, rather than merely rendering differently.

| Concern | MudBlazor | Radzen |
|---|---|---|
| Validation | `MudForm` per-field hook | `EditContext` + `GKitFluentValidator<T>` |
| Validator registration | injected by concrete type | also needs `IValidator<T>` — use `AddGKitValidator` |
| Field identity for validation | `For="@(() => Model.X)"` | `Name="X"` |
| Dialog closing | cascaded dialog instance | `DialogService.Close` — hence `IUiDialogHost` in DI |
| Dismissal | explicit cancelled flag | `null` result |
| Column resize | `ColumnResizeMode` enum | `AllowColumnResize` bool |
| Row click | `OnRowClick` with event args | `OnRowClick` with the item |
| Selection | `HashSet<T> SelectedItems` | `IList<T> SelectedItems` |
| Its own strings | needs `MudBlazor.Translations` | built in (`de`, `es`, `fr`, `it`, `ja`) |

### Filtering is not identical

Both adapters carry their own library's filter application across as a delegate, so each behaves
exactly as that library intends — but they do not agree with each other. Radzen defaults to
case-insensitive comparison via `FilterCaseSensitivity.Default`; MudBlazor defers to the database
collation. The same typed text can match different rows. `GridFilterOperator` is the intersection
of the two operator sets; Radzen's `Custom`, `In` and `NotIn` map to `Unknown` in the neutral
projection, which affects inspection only, never the query.

### String property paths

Radzen columns take a string path (`Property="Category.Name"`) and its sorter cannot evaluate
method calls. A MudBlazor column written as `Property="x => x.MainSite()!.Address"` needs either
`SortProperty`/`FilterProperty` overrides or a mapped shadow property when ported.

## Writing portable pages

Data loading should be written against the neutral model, which compiles unchanged under either
adapter:

```cs
private async Task<GridPage<Widget>> LoadAsync(GridQuery<Widget> state, CancellationToken token)
{
  await using var ctx = await Factory.CreateDbContextAsync(token);

  var query = state.ApplyFilters(ctx.Set<Widget>());
  var total = await query.CountAsync(token);

  return new GridPage<Widget>
  {
    TotalItems = total,
    Items = await state.ApplySorts(query).Skip(state.StartIndex).Take(state.Count).ToListAsync(token)
  };
}
```
