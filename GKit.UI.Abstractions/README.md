# GKit.UI.Abstractions

UI-neutral contracts shared by the MudBlazor and Radzen GKit adapters. This package ships no
components and references no component library.

An application normally references an adapter (`GKit.UI.MudBlazorExt` or `GKit.UI.RadzenExt`) rather
than this package directly. Reference it on its own when you want to write code - query
factories, report dialogs, row-control descriptors - that compiles against either adapter.

## Grid state

`GridQuery<T>`, `GridPage<T>`, `GridFilterSet<T>`, `GridFilter`, `GridSort` replace the
component-library grid state types.

```cs
// Works under either adapter
private async Task<GridPage<Company>> LoadAsync(GridQuery<Company> state, CancellationToken token)
{
  var query = state.ApplyFilters(ctx.Set<Company>());
  var total = await query.CountAsync(token);

  return new GridPage<Company>
  {
    TotalItems = total,
    Items = await state.ApplySorts(query).Skip(state.StartIndex).Take(state.Count).ToListAsync(token)
  };
}
```

`GridFilterSet<T>.Apply` runs a delegate the adapter captured from its own library, so filtering
behaves exactly as that library intends. `Filters` and `Sorts` are neutral projections for
inspection and logging - pass a `GridFilterSet<T>` to a report dialog instead of a
library-specific filter definition list.

## Shell services

`IUiNotifier`, `IUiDialogs`, `IUiDialogHost`, `IUiFormHandle`, `IUiLookupHandle<T>` abstract
snackbars, dialogs and form validation. Each adapter registers its own implementations.

## Entities and validation

`IEditEntityDialog<T>`, `IEditEntityForm<T>`, `NewValueResult<N>` and `AbstractValidatorBase<T>`
are shared unchanged between adapters - your validators and dialog subclasses compile against
both.

## Theming

`UiColor`, `UiIcon` and `IUiIconSet` cover what GKit's own components render. Anything outside
the shared icon set goes through `GridRowControl<T>.IconOverride` as a library-native identifier.

## Localization

`IGKitUiStrings` declares every string GKit renders, as named members so a missing translation is
a compile error. `DefaultUiStrings` supplies English. Reference `GKit.UI.Localization` for
culture-aware resources including Italian.
