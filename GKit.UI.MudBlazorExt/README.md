# GKit.UI.MudBlazorExt

MudBlazor adapter for the GKit UI components. Succeeds `GKit.MudBlazorExt`.

The components here are presentation only; paging, the DbContext lifecycle, CRUD orchestration,
ordering and export live in `GKit.UI.Data` and reference no component library. `GKit.UI.RadzenExt` is
the sibling adapter over the same engine.

An application picks **one** adapter. Both expose the same component names, type parameters and
parameter names, so moving a page between them is a package swap, an `@using` change, and a
rewrite of the column markup and form bodies — see `GKit.UI.RadzenExt`'s README for the differences
that survive the abstraction.

## Setup

```cs
builder.Services.AddMudServices();          // MudBlazor's own setup
builder.Services.AddMudTranslations();      // optional, MudBlazor's internal strings
builder.Services.AddGKitBlazorServices();   // GKit.BlazorExt
builder.Services.AddGKitMudBlazorUi();      // this package (calls AddGKitUiCore internally)
builder.Services.AddGKitUiLocalization();   // optional, for Italian
```

```razor
@* _Imports.razor *@
@using MudBlazor
@using GKit.BlazorExt
@using GKit.UI
@using GKit.UI.Data
@using GKit.UI.MudBlazorExt
```

## Components

| Component | Purpose |
|---|---|
| `ManagedGrid<T>` | Virtualised server-side MudDataGrid with toolbar, row controls and XLSX export |
| `EntityGrid<T, TDialog>` | `ManagedGrid` plus create / edit / delete against an EF entity |
| `EditEntityDialog<T, TForm, TValidator>` | Dialog shell hosting a form, validated by FluentValidation |
| `EntityAutocomplete<T>` / `EntityItemAutocomplete<T, TItem>` | Lookup over a DbContext, with inline create |
| `ManagedAutocomplete<T>` / `ManagedItemAutocomplete<T, TItem>` | Lookup over an arbitrary fetch delegate |
| `DataAutocomplete<T>` | Lookup over a pre-built query |
| `ManagedText` | Text with copy-to-clipboard and optional masking |

## Migrating from GKit.MudBlazorExt

The namespace changed and the grid state types are now neutral, so that the same application code
compiles against either adapter.

| Before | After |
|---|---|
| `namespace GKit.MudBlazorExt` | `namespace GKit.UI` (contracts) / `GKit.UI.MudBlazorExt` (components) |
| `ManagedGridRowControlDescriptor<T>` | `GridRowControl<T>` |
| `ManagedGridRowControlsVariant` | `GridRowControlsVariant` |
| `CellContext<T>` in row controls | `RowContext<T>` |
| `Color.Error` | `UiColor.Error` |
| `Icons.Material.Filled.Edit` | `UiIcon.Edit`, or `IconOverride = Icons.Material.Filled.Edit` |
| `GridStateVirtualize<T>` / `GridData<T>` in `LoadServerData` | `GridQuery<T>` / `GridPage<T>` |
| `QueryFilterExtensions.Where(q, state.FilterDefinitions)` | `state.ApplyFilters(q)` |
| `grid.FilterDefinitions` | `grid.Filters` (a `GridFilterSet<T>`) |
| `IEnumerable<IFilterDefinition<T>>` parameters | `GridFilterSet<T>` |
| `AbstractValidatorBase<T>`, `IEditEntityForm<T>`, `IEditEntityDialog<T>` | unchanged, now in `GKit.UI` |

Validator classes, `EditEntityDialog` subclasses, form `@code` blocks and query factories need no
changes beyond the `@using`.

### Loading data

```diff
- private async Task<GridData<Item>> LoadServerData(GridStateVirtualize<Item> state, CancellationToken token)
+ private async Task<GridPage<Item>> LoadServerData(GridQuery<Item> state, CancellationToken token)
  {
    using var ctx = await factory.CreateDbContextAsync();
    var query = ctx.Set<Item>().AsQueryable();
-   query = QueryFilterExtensions.Where(query, state.FilterDefinitions);
-   query = QuerySortExtensions.OrderBy(query, state.SortDefinitions);
+   query = state.Apply(query);

-   return new GridData<Item> { TotalItems = await query.CountAsync(token), Items = … };
+   return new GridPage<Item> { TotalItems = await query.CountAsync(token), Items = … };
  }
```

### Row controls

```diff
- new ManagedGridRowControlDescriptor<Item> {
+ new GridRowControl<Item> {
    Text = "Download",
-   Icon = Icons.Material.Filled.Download,
-   Color = Color.Primary,
+   Icon = UiIcon.Download,
+   Color = UiColor.Primary,
-   Action = ctx => DoAsync(ctx.Item)
+   Action = ctx => DoAsync(ctx.Item)      // ctx is now RowContext<Item>
  }
```

## Behaviour notes

- **Validation is unchanged.** MudForm still drives validation, so the touched-gated save button
  and `ValidationDelay` behave exactly as before. Only the Radzen adapter uses `EditContext`.
- **Filtering and sorting are unchanged.** `GridQuery<T>` carries MudBlazor's own filter and sort
  application across as delegates rather than reinterpreting them, so a filtered grid produces the
  same SQL as before.
- **Strings default to English.** Reference `GKit.UI.Localization` and run under `it-IT` for the
  previous Italian wording.
- **Delete confirmations are HTML-encoded.** The entity description interpolated into the
  confirmation prompt used to be raw, which made any user-controlled field a stored-XSS sink.
