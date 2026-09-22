# Proposal — a Radzen UI option for GKit, alongside MudBlazor

Status: proposal, rev 3 (branch `dev-radzen`)
Scope: `GKit.MudBlazorExt` and everything downstream of it.

**Decisions taken**

1. Naming follows the `GKit.UI.<subpackage>` convention.
2. Radzen **components** only — the Radzen Studio designer is not in play.
3. Both adapters are published and maintained; **an application picks one**. The two libraries coexist as alternatives but are not used together in the same app. *(rev 3 — supersedes the mixing requirement of rev 2.)*
4. AllGlass / BFer / FabbriFC / Millesrl / Stuff are **reference material only**, used to infer real GKit usage. They are not migration targets under this proposal.
5. A **translation layer/contract** is in scope, delivered as its own `GKit.UI.*` package.
6. The MudBlazor adapter is renamed **`GKit.UI.MudBlazorExt`**; the old `GKit.MudBlazorExt` ID is published once as deprecated.
7. Default culture is the **framework neutral default (English)**, with `it-IT` as an opt-in resource in `GKit.UI.Localization`.
8. Radzen is pinned to the **latest stable, 11.4.1**.

Rev 3 note: dropping in-app mixing removes the four hardest problems in rev 2 — Razor tag ambiguity, per-adapter DI resolution, dialog/shell pairing, and CSS coexistence — and lets the MudBlazor adapter keep its current validation behaviour unchanged (§11 Q1 is now closed). The neutral core below survives intact, for the reasons in §2.

---

## 1. What the reference projects actually use

Three of the five repos consume `GKit.MudBlazorExt`:

| Repo | Package | GKit.MudBlazorExt | MudBlazor |
|---|---|---|---|
| AllGlass | `ErpPalletLink` | 0.0.32 | 9.6.0 |
| BFer | `Benati.Rifiuti.WebApp` | 0.0.32 | 9.5.0 |
| Stuff | `StuffHR` | 0.0.35 | 9.9.0 |
| FabbriFC | `FC.TimeTracker` | — | 6.19.1 |
| Millesrl | `TLM.Web`, others | — | 6.11.0 / 6.3.1 |

Measured API usage across the three:

```
35  EntityGrid                     19  ManagedGridRowControlDescriptor
35  EntityAutocomplete             17  IEditEntityForm
20  AbstractValidatorBase          17  EditEntityDialog
15  ManagedGrid                    10  ManagedItemAutocomplete
 5  ManagedGridRowControlsVariant   4  ManagedText / ManagedAutocomplete
 1  ToXlsAsync                      1  DataAutocomplete
11  direct QueryFilterExtensions.Where(query, …FilterDefinitions)   ← MudBlazor leaking into app code
```

All three call `AddMudServices()` + `AddMudTranslations()` in `Program.cs` — relevant to §6.

That last usage row is the key observation. `QueryFilterExtensions`, `IFilterDefinition<T>`, `GridStateVirtualize<T>`, `GridData<T>`, `SortDefinition<T>`, `CellContext<T>`, `Color` and `Icons.Material.*` appear **in application signatures** — `WasteMovementReportDialog.FilterDefinitions`, `AttendanceReportDialog.FilterDefinitions`, every `LoadServerData` body.

## 2. Why a neutral core still earns its keep

Without mixing, the obvious cheap route is to fork `GKit.MudBlazorExt` into a Radzen twin and maintain both. That is worse than it looks, and the shared core is still justified on four grounds:

1. **A project can change its mind.** Today the data plumbing above is typed against MudBlazor, so switching a project's UI library means rewriting its query factories, `LoadServerData` bodies and report dialogs. With the neutral core, switching is confined to markup.
2. **Fixes land once.** The DbContext lifecycle, shared-context locking and cancel/abort swallowing in `EntityGrid.razor.cs` are subtle and have clearly been iterated on. Two copies drift.
3. **New features land once.** Anything added to the CRUD engine, export or ordering benefits both adapters for free.
4. **Localization lands once** (§6), rather than being translated twice.

The shared core is also what makes the two adapters *genuinely* interchangeable rather than merely similarly named.

## 3. Where the seam is

Reading `GKit.MudBlazorExt` file by file, the coupling is shallower than the file list suggests:

| File | Lines | MudBlazor coupling |
|---|---|---|
| `AbstractValidatorBase.cs` | 13 | **none** — pure FluentValidation |
| `IEditEntityDialog.cs` | 11 | **none** — EF only |
| `IEditEntityForm.cs` | 21 | **none** — EF only |
| `EntityGrid.razor.cs` | 302 | ~10%: `ISnackbar`, `IDialogService`, `DialogParameters`, `GridData`, `GridStateVirtualize` in signatures. DbContext lifecycle, shared-context `SemaphoreSlim`, cancel/abort swallowing, `IgnoreQueryFilters`/`AsNoTracking`/`ChangeTracker.Clear()` and the CRUD orchestration are all neutral. |
| `ManagedGrid.razor.cs` | 122 | ~20%: `CellContext<T>`, `Color`, `DataGridRowClickEventArgs` |
| `GridQueryDataExtensions.cs` | 64 | only `SortDefinition<T>` + `QuerySortExtensions` (a ~20-line `OrderBy`/`ThenBy` chainer) |
| `MudDataGridExtensions.cs` | 33 | reads `grid.RenderedColumns` to produce `(Title, PropertyPath)` pairs; rest is `GKit.Reporting` |
| `*.razor` markup | ~650 | 100% — genuinely per-library |

**The behaviour is portable; the markup is not.** This proposal makes that split structural.

## 4. Proposed package set

### New

| Package | Purpose | Dependencies |
|---|---|---|
| **`GKit.UI.Abstractions`** | UI-neutral models, service contracts, `IGKitUiStrings` + built-in default strings. Ships no widgets. | `Microsoft.AspNetCore.Components.Web` |
| **`GKit.UI.Data`** | EF-backed engine: grid loading, DbContext lifecycle, CRUD orchestration, XLSX export, null-safe ordering. | `GKit.UI.Abstractions`, `GKit.BlazorExt`, `GKit.Reporting`, `Microsoft.EntityFrameworkCore`, `FluentValidation` |
| **`GKit.UI.Localization`** | RESX/`IStringLocalizer`-backed `IGKitUiStrings` (it-IT, en-US) plus a Radzen component-text bundle. | `GKit.UI.Abstractions`, `Microsoft.Extensions.Localization` |
| **`GKit.UI.MudBlazorExt`** | MudBlazor adapter. Succeeds `GKit.MudBlazorExt`. | `GKit.UI.Data`, `MudBlazor` |
| **`GKit.UI.RadzenExt`** | Radzen adapter. | `GKit.UI.Data`, `Radzen.Blazor` |

The two adapters use **identical component names in distinct namespaces**. Since an app references exactly one of them, there is no tag ambiguity and no need for prefixed aliases — switching library is a package swap plus one `@using` line in `_Imports.razor`.

### Why the `Ext` suffix, not `GKit.UI.MudBlazor`

The first cut named these `GKit.UI.MudBlazor` and `GKit.UI.Radzen`. That is a namespace-shadowing
trap, verified against the compiler rather than reasoned about:

C# resolves a `using` directive by searching enclosing namespace declarations before the global
namespace, and Razor emits its usings **inside** the generated namespace. So from anywhere under
`GKit.UI.*`, the name `MudBlazor` finds `GKit.UI.MudBlazor` first and binds to it instead of the
library.

| Scenario | `GKit.UI.MudBlazor` | `GKit.UI.MudBlazorExt` |
|---|---|---|
| The adapter's own `.razor` files, plain `@using MudBlazor` | ❌ binds to itself | ✅ |
| Ordinary consumer (`Acme.Erp.Web`) | ✅ | ✅ |
| Consumer whose own namespace is under `GKit.UI.*` | ❌ **fails to compile** | ✅ |

Ordinary consumers were never at risk — the failure needs a namespace under `GKit.UI`. But the
adapter itself always is, which is why the first cut needed `@using global::MudBlazor` in every
`_Imports.razor`: a permanent workaround, and a trap for anyone adding a file to those projects.

The `Ext` suffix removes the shadowing entirely, the `global::` qualifiers are gone, and the names
sit closer to the original `GKit.MudBlazorExt` — which also makes the migration read more
naturally. Note that `.cs` files were never affected: they put usings *outside* the namespace
declaration, which is why the problem only ever showed up in Razor.

The same applies to any project whose own name ends in a dependency's root namespace —
`Test.Repo.UI.Radzen`, the demo host, still needs `@using global::Radzen` for exactly this reason.
That one is the host's own naming, not the package's.

### Changed / retired

- **`GKit.MudBlazorExt`** — renamed to `GKit.UI.MudBlazorExt` for symmetry with `GKit.UI.RadzenExt`; the old package ID published once more as deprecated, pointing at the new one. (Type-forwarding won't work across the namespace change, so this is a marker, not a compat shim. Confirm in §11.)
- **`Test.Repo.UI`** — becomes the MudBlazor host.
- **`Test.Repo.UI.Shared`** (new RCL) — demo domain, validators, forms, dialog subclasses, query factories: the part that must compile against both.
- **`Test.Repo.UI.Radzen`** (new) — the Radzen host, rendering the same pages from the same shared RCL.

### Unchanged

`GKit.BlazorExt` (already neutral), `GKit.Reporting`, `GKit.Authentication.Blazor` (no MudBlazor reference — `LoginComponentBase`/`LogoutComponentBase`/`RedirectToLogin` are markup-free), `GKit.SmartCardHost.Blazor`, and every non-UI package.

```
GKit.BlazorExt ───────────────┐
GKit.Reporting ───────────────┤
GKit.UI.Abstractions ─────────┴──> GKit.UI.Data ──┬──> GKit.UI.MudBlazorExt ──> app (Mud)
        │                                         └──> GKit.UI.RadzenExt    ──> app (Radzen)
        └──────> GKit.UI.Localization ──────────────────────> (either)
```

Three shared packages rather than one: `GKit.UI.Abstractions` stays free of EF Core and NPOI so an app can reference it alone to write UI-neutral code; `GKit.UI.Localization` stays optional so an app happy with the built-in Italian defaults pays nothing.

## 5. The neutral contracts (`GKit.UI.Abstractions`)

### 5.1 Grid state — replaces `GridStateVirtualize<T>` / `GridData<T>` / `IFilterDefinition<T>`

```csharp
public sealed record GridSort(string Path, bool Descending);

public enum GridFilterOperator {
  Contains, NotContains, Equal, NotEqual, StartsWith, EndsWith,
  GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual,
  Empty, NotEmpty, IsNull, IsNotNull
}

public sealed record GridFilter(string Path, GridFilterOperator Operator, object? Value, Type? PropertyType = null);

public sealed class GridFilterSet<T> {
  public IReadOnlyList<GridFilter> Filters { get; init; } = [];
  internal Func<IQueryable<T>, IQueryable<T>>? Native { get; init; }
  public IQueryable<T> Apply(IQueryable<T> source) => Native?.Invoke(source) ?? source;
}

public sealed class GridQuery<T> {
  public int StartIndex { get; init; }
  public int Count { get; init; }
  public GridFilterSet<T> Filters { get; init; } = new();
  public IReadOnlyList<GridSort> Sorts { get; init; } = [];
  internal Func<IQueryable<T>, IQueryable<T>>? NativeSort { get; init; }

  public IQueryable<T> ApplyFilters(IQueryable<T> s) => Filters.Apply(s);
  public IQueryable<T> ApplySorts(IQueryable<T> s)   => NativeSort?.Invoke(s) ?? s;
  public IQueryable<T> Apply(IQueryable<T> s)        => ApplySorts(ApplyFilters(s));
}

public sealed class GridPage<T> {
  public IReadOnlyList<T> Items { get; init; } = [];
  public int TotalItems { get; init; }
  public static GridPage<T> Empty { get; } = new();
}
```

**The `Native` delegate is the load-bearing idea.** Rather than reimplementing Mud's `IFilterDefinition.GenerateFilterExpression()` and Radzen's Dynamic-LINQ filter strings in neutral code — a re-implementation that could never be faithful to either — each adapter *captures its own library's filter/sort application* as a closure and hands it to the engine. The engine applies it without knowing what is inside. The neutral `GridFilter`/`GridSort` records are still projected so app code can inspect them (report dialogs, logging) with no library reference.

This is what converts the 11 leaking call sites:

```diff
- query = QueryFilterExtensions.Where(query, gridState.FilterDefinitions);
+ query = gridState.ApplyFilters(query);

- [Parameter] public IEnumerable<IFilterDefinition<WasteMovement>> FilterDefinitions { get; set; } = [];
+ [Parameter] public GridFilterSet<WasteMovement> Filters { get; set; } = new();
```

Identical source under either library — which is §2's point 1, made concrete.

### 5.2 Shell services — replace `ISnackbar` / `IDialogService` / `IMudDialogInstance`

```csharp
public interface IUiNotifier { void Success(string m); void Error(string m); void Warning(string m); void Info(string m); }

public sealed record UiDialogResult(bool Canceled, object? Data);

public interface IUiDialogs {
  Task<UiDialogResult> ShowAsync(Type component, string title, IReadOnlyDictionary<string, object?> parameters);
  Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText);
}

public interface IUiDialogHost { void Close(object? result); void Cancel(); }   // cascaded into the dialog
public interface IUiFormHandle { bool IsTouched { get; } bool IsValid { get; } Task<bool> ValidateAsync(); Task ResetAsync(); }
public interface IUiLookupHandle<T> { Task CloseDropDownAsync(); Task SelectAsync(T value); }
```

| Contract | MudBlazor impl | Radzen impl |
|---|---|---|
| `IUiNotifier` | `ISnackbar.Add(msg, Severity.X)` | `NotificationService.Notify(new NotificationMessage { Severity = … })` |
| `IUiDialogs` | `IDialogService.ShowAsync` / `ShowMessageBoxAsync` | `DialogService.OpenAsync` / `DialogService.Confirm` |
| `IUiDialogHost` | wraps `IMudDialogInstance` | wraps `DialogService.Close(result)` |
| `IUiFormHandle` | wraps `MudForm` | wraps `EditContext` (`IsTouched` → `IsModified()`) |
| `IUiLookupHandle<T>` | `CloseMenuAsync()` / `SelectOptionAsync(v)` | set `Value`, `await grid.Reload()` |

Because only one adapter is present per app, these are registered as the interfaces themselves and resolved from DI normally:

```csharp
builder.Services.AddGKitUiCore();          // GKit.UI.Data engines, default IGKitUiStrings
builder.Services.AddGKitUiLocalization();  // optional: RESX-backed IGKitUiStrings
builder.Services.AddGKitMudBlazorUi();     // -- or --
builder.Services.AddGKitRadzenUi();        // registers IUiNotifier, IUiDialogs, IUiIconSet
```

Asymmetry the adapter absorbs: Mud returns a `DialogResult` with an explicit `Canceled` flag; Radzen's `OpenAsync` returns `dynamic` where `null` means dismissed. The Radzen adapter maps `null → Canceled = true`.

### 5.3 Icons, colours, row controls

```csharp
public enum UiColor { Default, Primary, Secondary, Info, Success, Warning, Error }
public enum UiIcon  { Add, Edit, Delete, Download, Refresh, More, DragIndicator,
                      Visibility, VisibilityOff, ContentCopy, Check, Close, Search }

public interface IUiIconSet { string Resolve(UiIcon icon); }
// Mud    -> Icons.Material.Filled.Edit  (SVG path)
// Radzen -> "edit"                      (Material Symbols ligature)

public sealed record RowContext<T>(T Item, int RowIndex);

public class GridRowControl<T> {
  public required string Text { get; set; }
  public required Func<RowContext<T>, Task> Action { get; set; }
  public UiIcon? Icon { get; set; }
  public string? IconOverride { get; set; }          // per-library escape hatch
  public UiColor Color { get; set; } = UiColor.Default;
  public Func<RowContext<T>, bool>? Disabled { get; set; }
}

public enum GridRowControlsVariant { Expanded, Menu }
```

## 6. Localization (`GKit.UI.Localization`)

Every user-facing string in `GKit.MudBlazorExt` is currently a hard-coded Italian literal. The complete inventory, extracted from the source:

| Category | Strings |
|---|---|
| Row / toolbar actions | `Modifica`, `Elimina`, `Aggiungi`, `Aggiorna`, `Esporta XLSX`, `Salva`, `Annulla`, `Ok`, `Apri Anagrafica` |
| Dialog titles | `Crea Elemento`, `Modifica Elemento`, `Conferma Operazione` |
| Confirmations | `Confermi l'operazione?`, `Confermi di voler eliminare <strong>{0}</strong>?` |
| Outcomes | `Elemento aggiunto con successo`, `Impossibile aggiungere l'elemento`, `Elemento modificato con successo`, `Impossibile modificare l'elemento`, `Elemento eliminato con successo`, `Impossibile eliminare l'elemento`, `Impossibile recuperare i valori` |
| Empty states | `Nessun elemento presente`, `Nessun elemento trovato` |
| Export | `Colonna {index}` |
| Accessibility | `Open user menu` — an English leftover on `ManagedGrid`'s row-actions `MudMenu` aria-label |

### Contract, in `GKit.UI.Abstractions`

```csharp
public interface IGKitUiStrings
{
  string Edit { get; } string Delete { get; } string Add { get; } string Refresh { get; }
  string ExportXlsx { get; } string Save { get; } string Cancel { get; } string Ok { get; }
  string OpenRegistry { get; } string RowActionsMenu { get; }

  string CreateItemTitle { get; } string EditItemTitle { get; } string ConfirmTitle { get; }

  string ConfirmGeneric { get; }
  string ConfirmDelete(string itemDescription);

  string ItemAdded { get; }   string ItemAddFailed { get; }
  string ItemUpdated { get; } string ItemUpdateFailed { get; }
  string ItemDeleted { get; } string ItemDeleteFailed { get; }
  string FetchValuesFailed { get; }

  string NoRecords { get; } string NoResults { get; }

  string ColumnFallback(int index);
}
```

Named members rather than string keys, so a missing translation is a compile error rather than a runtime `???`.

### Default culture is neutral English

A `DefaultUiStrings` implementation in `GKit.UI.Abstractions` supplies **English** — the .NET convention for a neutral fallback resource — so the base packages carry no localization dependency and a new consumer gets sensible strings out of the box. Italian ships as an opt-in resource in `GKit.UI.Localization`.

**This is a deliberate behaviour change.** Today every one of these strings is Italian, hard-coded. Under this decision an app gets Italian only if it references `GKit.UI.Localization` and runs under `it-IT`:

```csharp
builder.Services.AddGKitUiLocalization();
// + CultureInfo.CurrentUICulture / RequestLocalizationOptions set to it-IT
```

Worth stating plainly because it is the one place in this proposal where an existing app's *visible output* changes rather than just its source. The English table below is the new default; the Italian column becomes the `it-IT` resource.

| Member | Default (en) | `it-IT` resource |
|---|---|---|
| `Edit` / `Delete` / `Add` | Edit / Delete / Add | Modifica / Elimina / Aggiungi |
| `Refresh` / `ExportXlsx` | Refresh / Export XLSX | Aggiorna / Esporta XLSX |
| `Save` / `Cancel` / `Ok` | Save / Cancel / OK | Salva / Annulla / Ok |
| `OpenRegistry` | Open registry | Apri Anagrafica |
| `CreateItemTitle` / `EditItemTitle` | Create item / Edit item | Crea Elemento / Modifica Elemento |
| `ConfirmTitle` / `ConfirmGeneric` | Confirm operation / Confirm this operation? | Conferma Operazione / Confermi l'operazione? |
| `ConfirmDelete(x)` | Delete **{0}**? | Confermi di voler eliminare **{0}**? |
| `ItemAdded` / `ItemAddFailed` | Item added / Could not add the item | Elemento aggiunto con successo / Impossibile aggiungere l'elemento |
| `ItemUpdated` / `ItemUpdateFailed` | Item updated / Could not update the item | Elemento modificato con successo / Impossibile modificare l'elemento |
| `ItemDeleted` / `ItemDeleteFailed` | Item deleted / Could not delete the item | Elemento eliminato con successo / Impossibile eliminare l'elemento |
| `FetchValuesFailed` | Could not load values | Impossibile recuperare i valori |
| `NoRecords` / `NoResults` | No records / No matches found | Nessun elemento presente / Nessun elemento trovato |
| `ColumnFallback(i)` | Column {i} | Colonna {i} |
| `RowActionsMenu` | Row actions | Azioni riga |

`RowActionsMenu` also fixes the stray English `"Open user menu"` aria-label currently on `ManagedGrid`'s row-actions menu, which was neither correct English for what it does nor Italian.

### Implementation, in `GKit.UI.Localization`

RESX + `IStringLocalizer<GKitUiStrings>`, neutral resource in English plus an `it` satellite, culture from `CultureInfo.CurrentUICulture` / `RequestLocalizationOptions`. Registered via `AddGKitUiLocalization()`. This mirrors how Radzen itself localizes (§6 below), so both layers respond to the same culture setting.

### Radzen's own strings — corrected in rev 4

Rev 2/3 claimed Radzen ships no equivalent of `MudBlazor.Translations` and that `GKit.UI.Localization` should therefore supply a `RadzenComponentTexts` bundle. **That is wrong for 11.4.1**, verified against the package:

```
pkg/lib/net10.0/{de,es,fr,it,ja}/Radzen.Blazor.resources.dll
```

Radzen ships **built-in satellite assemblies including Italian**, resolved from `CultureInfo.CurrentUICulture` with no registration call. The `it` resources cover the grid's filter vocabulary — `contiene`, `Inizia con`, `Maggiore o uguale`, `Applica`, `Cancella filtro`, `Colonne`, `Filtro applicato` — i.e. exactly the 29 `*Text` parameters on `RadzenDataGrid` (`ContainsText`, `StartsWithText`, `ApplyFilterText`, `ClearFilterText`, `EmptyText`, `AllColumnsText`, …).

Consequences:

- **The `RadzenComponentTexts` bundle is dropped.** It would duplicate work Radzen already does, and would have to be maintained against Radzen's own wording.
- The asymmetry runs the *other* way: MudBlazor needs the separate `MudBlazor.Translations` package, Radzen needs nothing.
- `GKit.UI.Localization` is still required, and for the reason that actually motivated it — **GKit's own 24 strings** (the table above), which neither library knows about.
- `GKit.UI.RadzenExt` will expose an **optional** override hook for apps that want wording different from Radzen's defaults, surfaced through `IGKitUiStrings` rather than set per grid. Optional, not a bundle.

Also confirmed: Radzen 11.4.1 exposes **no `IStringLocalizer` hook** in its public API — localization is plain satellite-assembly resolution. Nothing in the design depends on one.

### One security note, found while inventorying

`EntityGrid.OnBeforeDelete` currently does:

```csharp
(MarkupString)$"Confermi di voler eliminare <strong>{ToStringFunc(entity)}</strong>?"
```

`ToStringFunc` returns entity-derived text (`p => p.CompanyName`, `p => p.ApplicationUser?.AccountName`) interpolated into a `MarkupString` **unencoded**. If any such field is user-supplied, this is a stored-XSS sink. The shared `ConfirmDelete(string)` implementation should HTML-encode the interpolated value. Small fix, worth doing while the code is being moved anyway.

## 7. The engine (`GKit.UI.Data`)

Moved essentially verbatim out of `EntityGrid.razor.cs` / `ManagedGrid.razor.cs`, with MudBlazor types swapped for neutral ones. No behavioural change intended.

```csharp
public sealed class EntityGridEngine<T> where T : class {
  // Func<DbContext> contextFactory, DbContext? sharedContext, Func<DbContext, IQueryable<T>> queryFactory
  public Task              WithDbContextAsync(Func<DbContext, Task> action, params object[] entities);
  public Task<R>           WithDbContextAsync<R>(Func<DbContext, Task<R>> action, params object[] entities);
  public Task<GridPage<T>> LoadAsync(GridQuery<T> query, CancellationToken token);
  public Task<IQueryable<T>> BuildExportQueryAsync(DbContext ctx, GridQuery<T> query);
}

public sealed class EntityCrudOrchestrator<T>(
    EntityGridEngine<T> engine, IUiDialogs dialogs, IUiNotifier notifier,
    IGKitUiStrings strings, ILogger logger) where T : class {
  public Task<bool> NewAsync(Type dialogType, Func<Task<NewValueResult<T>>>? newValueFactory);
  public Task<bool> EditAsync(Type dialogType, T entity);
  public Task<bool> DeleteAsync(T entity, Func<T, string>? toString);
}

public sealed class EditEntityDialogEngine<T> where T : class {
  public Task SubmitAsync(IUiFormHandle form, IEditEntityForm<T>? instance, IUiDialogHost host, T model);
  public Task CancelAsync(IUiFormHandle form, IUiDialogHost host);
}

public sealed class EntityLookupEngine<T, TItem> where T : class {
  public Func<string, CancellationToken, Task<IReadOnlyList<TItem>>> Fetch { get; init; }
  public Func<TItem, T> ToValue { get; init; }
  public Func<T, TItem> ToItem  { get; init; }
  public TItem? GetByValue(T value);    // holds the FilteredItems cache
  public string Display(T value);
}

public sealed record ExportColumn(string Title, string PropertyPath);
public static class XlsExportExtensions {
  public static Task ToXlsAsync<T>(this IQueryable<T> q, string title, IEnumerable<ExportColumn> cols, Stream output);
}

public static class QueryOrderExtensions {
  public static IQueryable<T> OrderBy<T>(this IQueryable<T> s, IEnumerable<GridSort> sorts);
  public static IQueryable<T> NullCheckingOrderBy<T>(this IQueryable<T> s, IEnumerable<GridSort> sorts);
}
```

`EntityGrid.razor.cs` lines 16–95 (shared-context `SemaphoreSlim`, `AttachRange` retry, preamble/epilogue) and 230–280 (the `TaskCanceledException` / `DbException("aborted"|"cancelled")` swallowing) land here untouched. `NullCheckingOrderBy` becomes neutral by building its `Expression` chain against `GridSort.Path`; the only loss is MudBlazor's `QuerySortExtensions.OrderBy` chainer, ~20 lines to reimplement. It also drops the stray `using NPOI.SS.Formula.Functions;` currently sitting in that file.

Each adapter converts its own rendered columns to `ExportColumn[]`:
- Mud: `grid.RenderedColumns.Where(c => c is not TemplateColumn<T>).Select(c => new ExportColumn(c.Title ?? c.PropertyName, c.PropertyName))`
- Radzen: `grid.ColumnsCollection.Where(c => c.Property is not null).Select(c => new ExportColumn(c.Title, c.Property))`

## 8. The two adapters, name-for-name

Identical component names, type parameters and parameter names, so switching a project is a package swap plus one `@using` — then only `<Columns>` blocks and form bodies get rewritten.

| Component | `GKit.UI.MudBlazorExt` renders | `GKit.UI.RadzenExt` renders |
|---|---|---|
| `ManagedGrid<T>` | `MudDataGrid` + `VirtualizeServerData` | `RadzenDataGrid` + `LoadData` + `AllowVirtualization` |
| `EntityGrid<T,TDialog>` | ⤷ + Mud dialogs/snackbar | ⤷ + Radzen dialogs/notifications |
| `EditEntityDialog<T,TForm,TValidator>` | `MudDialog` + `MudForm` | `RadzenTemplateForm` + `GKitFluentValidator` |
| `EntityAutocomplete<T>` / `EntityItemAutocomplete<T,TItem>` | `MudAutocomplete` | `RadzenDropDownDataGrid` |
| `ManagedAutocomplete` / `ManagedItemAutocomplete` / `DataAutocomplete` | `MudAutocomplete` | `RadzenAutoComplete` |
| `ManagedText` | `MudText` + `MudIconButton` | `RadzenText` + `RadzenButton` |

Grid parameter mapping (unchanged on the GKit side):

| GKit parameter | MudBlazor | Radzen |
|---|---|---|
| virtualised server load | `VirtualizeServerData` + `GridStateVirtualize` | `LoadData` + `LoadDataArgs.Skip/Top` + `Count` |
| `DragDropColumnReordering` | `DragDropColumnReordering` | `AllowColumnReorder` |
| `ColumnResizeMode` | `ColumnResizeMode` | `AllowColumnResize` |
| `MultiSelection` / `SelectedItems` | `MultiSelection` / `SelectedItems` | `SelectionMode.Multiple` / `Value` |
| `Loading` | `Loading` | `IsLoading` |
| `RefreshDataAsync()` | `ReloadServerData()` | `Reload()` |
| multi-column sort | `SortMode.Multiple` | `AllowMultiColumnSorting` |

**Validation is fully shared, with no behavioural drift.** `AbstractValidatorBase<T>` moves to `GKit.UI.Abstractions` and both adapters consume the *same* validator classes. The MudBlazor adapter keeps `MudForm.Validation="@((object?)validator.ValidateValueAsync)"` exactly as today, including `IsTouched`-gated Save and `ValidationDelay`. Radzen has no per-field hook equivalent, so `GKit.UI.RadzenExt` ships a ~60-line `GKitFluentValidator` component subscribing to `EditContext.OnValidationRequested` / `OnFieldChanged` and writing into a `ValidationMessageStore`. `IUiFormHandle` hides the difference from the shared `EditEntityDialogEngine`. Every `CompanyValidator`, `DriverValidator`, … compiles against both untouched.

*(This is the point rev 2 had to leave open: with mixing dropped, the Mud adapter no longer needs to move to `EditContext`, so its current behaviour is preserved.)*

## 9. What breaks, and what does not

### Unchanged in application code

- Every `EditEntityDialog<T,TForm,TValidator>` subclass and its `EmptyValueFactory()`.
- Every `AbstractValidatorBase<T>` subclass.
- Every `Func<DbContext, IQueryable<T>> QueryFactory` — the `.Include().WithoutDeleted().WithoutNotActive()` chains.
- `IEditEntityForm<T>` `@code` blocks (hooks, model, context).
- `DbContextProvider<TCtx>` inheritance, `GKit.BlazorExt` services, `GKit.Reporting` usage.
- MudBlazor form/validation semantics (§8).

### Bugs the Radzen adapter exposed (phase 2)

Two more surfaced while building the second adapter, both in code I had just written — and both
invisible until something actually rendered:

1. **Seeding `Data` with an empty list silently disabled server-side loading.** Radzen fires its
   initial `LoadData` only when the grid has no data yet. `protected IEnumerable<T> Data { get; set; } = [];`
   looks harmless and meant `LoadData` never fired at all — an empty grid in production, not just
   in tests. `Data` now starts null, with a comment saying why.

2. **`[EditorRequired]` on an inherited parameter misfires.** `EntityGrid` supplies its own
   `LoadServerData` and `EntityAutocomplete` overrides `FetchAsync`, but the attribute is
   inherited, so every consumer got a spurious warning. Replaced with runtime guards in both
   adapters.

### Bugs the extraction exposed

Making the engine renderer-free meant it could be unit tested for the first time. Two real
defects surfaced immediately, both fixed in phase 0:

1. **Cancellation was never caught for EF queries.** The grid caught `TaskCanceledException`, but
   EF Core's own `ThrowIfCancellationRequested` raises the *base* `OperationCanceledException`.
   Since `TaskCanceledException` derives from it, catching the derived type let the common case
   through — so a virtualised grid abandoning a load as the user scrolled or typed into a filter
   would surface an unhandled error rather than resolving quietly. Now caught on
   `OperationCanceledException`. Found by `LoadAsync_CancelledToken_ReturnsEmptyPageRatherThanThrowing`.

2. **`AsNoTracking()` was discarded.** The load path read
   `if (!IsSharedContext()) query.AsNoTracking();` — the returned queryable was thrown away, so
   tracking stayed on. This contradicted the surrounding design, which attaches entities
   explicitly for edit and delete precisely because loads are meant to be untracked. Now assigned.

3. **The delete confirmation was an XSS sink** (§6), now HTML-encoded.

### Breaking changes

Source compatibility for the state/descriptor types is **not** preserved. The shims required (`new`-shadowed members on a generic base, or a base generic over context *and* colour type) are worse than the migration, and the `GKit.MudBlazorExt` → `GKit.UI.MudBlazorExt` rename means one migration rather than two.

| Before | After |
|---|---|
| `CellContext<T>` in descriptors | `RowContext<T>` |
| `Color.Error` | `UiColor.Error` |
| `Icons.Material.Filled.Edit` | `UiIcon.Edit` (or `IconOverride = Icons.…`) |
| `GridStateVirtualize<T>` / `GridData<T>` | `GridQuery<T>` / `GridPage<T>` |
| `QueryFilterExtensions.Where(q, s.FilterDefinitions)` | `s.ApplyFilters(q)` |
| `IEnumerable<IFilterDefinition<T>>` parameters | `GridFilterSet<T>` |
| `namespace GKit.MudBlazorExt` | `namespace GKit.UI.MudBlazorExt` |

Per decision 4 there are no in-scope consumers to migrate; the reference apps quantify the cost if they are ever migrated separately (~65 mechanical sites across three repos).

### Honest asymmetries the shared core will *not* hide

1. **Filter semantics differ.** Mud builds `Expression<Func<T,bool>>` against DB collation; Radzen defaults to case-insensitive `ToLower()` comparisons via Dynamic LINQ. The same typed text can match different rows. `GridFilterOperator` is the intersection of the two operator sets.

2. **Method calls in property expressions do not port.** BFer's `CompanyGrid` uses `Property="x => x.MainSite()!.AddressNumber"` — a *method* invocation. Radzen columns take a string path and its Dynamic-LINQ sorter/filterer cannot call `MainSite()`. Such columns need `SortProperty`/`FilterProperty` overrides or a mapped shadow property. Also present in `WasteMovementGrid` and AllGlass's `Operations.razor` (`x => x.CompletedProduction!.Id` with `SortBy="x => null"`).

3. **Nested `MudForm` per collection item has no Radzen analogue.** `EditCompanyForm` renders one `MudForm` per `CompanySite` and per `CompanyNote` with `IsTouchedChanged` bubbling. Radzen has one `EditContext` per form. The idiomatic port collapses these into a single form with `RuleForEach(x => x.Sites).SetValidator(new CompanySiteValidator())` — arguably cleaner, but a rewrite of that form rather than a swap.

4. ~~**Autocomplete `BeforeItemsTemplate` / `NoItemsTemplate` have no Radzen equivalent.**~~ **Wrong — corrected in phase 2.** Radzen's `DropDownBase` has `HeaderTemplate`, so the "add" and "open registry" affordances render *inside* the popup exactly as they do under MudBlazor. No adjacent-button workaround was needed.

   A different divergence took its place, then got designed away: **virtualisation cannot render without a browser.** It is Blazor's `Virtualize`, which requests items only after JS measures the viewport — so a virtualised grid renders no rows under component tests or prerendering. That is true of *both* libraries, not just Radzen.

   **Both adapters now page by default and expose an opt-in `Virtualize` parameter.** Paging is also the conventional Radzen server-side pattern, and it means a grid renders rows in tests and during prerender. `GridPagingModeTest` pins the default down, including the MudBlazor page-index → skip/take translation.

5. **Cosmetic grid parameters** (`ShowColumnOptions`, `DragIndicatorIcon`, `ApplyDropClassesOnDragStarted`, `ItemSize`, `Dense`/`Hover`) are Mud-only; Radzen's nearest are `AllowColumnPicking` and `Density`. These become optional no-op-on-Radzen parameters.

6. **Page markup is not shared.** `MudStack`, `MudCard`, `MudTextField`, `MudCheckBox`, `MudTooltip`, `MudFab`, `MudDatePicker` appear throughout. GKit deliberately does **not** wrap every input control — that would be a large, lossy re-implementation of two design systems, and it would erase precisely the per-library character that motivates offering a choice.

## 10. How much is shared — measured, not estimated

Both adapters are now built, so these are counted rather than projected (`.cs` + `.razor`,
excluding `bin`/`obj`; the rev-2 estimates are shown for comparison).

| | lines | estimated | |
|---|---|---|---|
| `GKit.UI.Abstractions` | 474 | ~280 | shared |
| `GKit.UI.Data` | 699 | ~450 | shared |
| `GKit.UI.Localization` | 99 | ~150 | shared |
| **shared total** | **1,272** | | |
| `GKit.UI.MudBlazorExt` | 1,268 | ~400 | Mud-only |
| `GKit.UI.RadzenExt` | 1,356 | ~450 | Radzen-only |

**33% of the library is shared, not the ~50% estimated.** The estimate was wrong in a specific
way worth recording: the shared core came out roughly as predicted in substance but carries far
more documentation, and both adapters are ~3× the guess because a faithful adapter needs the full
parameter surface, state translation and DI wiring — not just markup. Counting a component library
adapter as "markup plus a bit of glue" understates it badly.

The ratio that actually matters is on the **application** side, where the picture is much better.
Comparing the two demo hosts' equivalent files:

| | Mud host | Radzen host |
|---|---|---|
| `WidgetGrid.razor` | 52 | 56 |
| `EditWidgetForm.razor` | 28 | 48 |
| `EditWidgetDialog.cs` | 14 | 14 |
| `Widgets.razor` | 20 | 20 |
| `WidgetReport.razor` | 48 | 48 |
| **total** | **162** | **186** |

against **158 lines of `Test.Repo.UI.Shared`** referenced unchanged by both — entities, DbContext,
query factories, validators and entity defaults.

More telling than the totals:

- `WidgetGrid.razor`'s entire `@code` block is **byte-identical** between the two hosts (verified
  by `diff` in `AdapterParityTest`). Only the `<Columns>` markup differs.
- `WidgetReport.razor` — the hand-written loader page — is **identical in full**, because its
  loader is typed against `GridQuery<T>`/`GridPage<T>` and never names a component library.
- `EditWidgetDialog.cs` differs only in which `using` it names.
- `EditWidgetForm.razor` is the honest cost: 28 → 48 lines, because Radzen needs explicit
  `RadzenFormField` wrappers and `ValidationMessage` components where MudBlazor's inputs carry
  labels and validation display themselves.

So: column markup and form bodies get rewritten, exactly as §9.6 predicted. Everything below them
does not.

### Optional further sharing — declarative columns (phase 3)

Many grids are pure property columns (BFer's `NationGrid`, `DisposalCodeGrid`, `UnitOfMeasureGrid`, `PhysicalStateGrid`, `RecoveryCodeGrid`, `StoreCauseGrid`). For those, a neutral descriptor makes the column set shared too:

```csharp
Columns="@ColumnSet.For<Nation>()
    .Text(p => p.Code, "Codice", filterable: true)
    .Text(p => p.Name, "Nome",   filterable: true)
    .Build()"
```

rendered by each adapter into its own column components — such a grid would then switch library with nothing but the `@using` change. Grids with `CellTemplate` markup keep native columns. Opt-in, not a prerequisite.

## 11. Sequencing

| Phase | Work | Outcome |
|---|---|---|
| 0 ✅ | Create `GKit.UI.Abstractions`, `GKit.UI.Data`, `GKit.UI.Localization`; move the neutral code out; rewrite the Mud adapter as `GKit.UI.MudBlazorExt`; retire `GKit.MudBlazorExt`. | **Done.** Solution builds with 0 errors and no warnings in any new project; 28 new unit tests pass; the 8 pre-existing integration failures are unchanged. |
| 1 ✅ | Split `Test.Repo.UI.Shared` out of `Test.Repo.UI` and give it real grids, forms and dialogs. | **Done.** The shared layer is a plain (non-Razor) library referencing neither adapter, so the boundary is compiler-enforced. 11 bUnit tests render the Mud components. |
| 2 ✅ | Build `GKit.UI.RadzenExt` + `Test.Repo.UI.Radzen` against the same `Test.Repo.UI.Shared`. | **Done.** Two hosts, one domain layer. 6 Radzen render tests + 10 parity tests. |
| 3 ✅ | Harden and document. | **Done.** Zero warnings in any new project; measured sharing figures in §10. |
| next | Declarative columns (§10), and whichever §9 asymmetry a real migration hits first. | Optional. |

**The coverage gap phase 0 left is closed.** `Test.Repo.UI` now renders `EntityGrid` and
`ManagedGrid` for real, and 17 bUnit tests across the two adapters assert on rendered output —
column headers, loaded rows, navigation properties, row controls, disabled predicates, click
callbacks and localized empty states.

**Dropped along the way:** the `RadzenComponentTexts` bundle (phase 0 — Radzen localizes itself,
§6), and the claim that Radzen dropdowns lack a header slot (phase 2 — they have one, §9.4).

Phases 0–1 were worth doing even if Radzen never shipped: they remove 11 MudBlazor leaks from the consumption surface, make the CRUD engine unit-testable without a renderer, fix the unencoded-`MarkupString` sink, and put the Italian strings behind a contract.

### Final state

| | |
|---|---|
| Build | 0 errors; all warnings are in pre-existing untouched projects (OpcUa, RENTRI, SmtpHost, Reporting, Authentication.Blazor) |
| Tests | 59 passing, 1 skipped, 8 failing — the same 8 pre-existing integration tests as the baseline, which need a RENTRI endpoint, PDF fixtures, an OPC UA server, PLC hardware and Active Directory |
| New tests | 55 (10 ordering, 11 engine, 7 strings, 11 Mud render, 6 Radzen render, 10 parity) |
| Packages | 5 new (`GKit.UI.Abstractions`, `.Data`, `.Localization`, `.MudBlazor`, `.Radzen`); `GKit.MudBlazorExt` retired |

## 12. Open questions — all closed

| Question | Resolution |
|---|---|
| Package rename | **`GKit.UI.MudBlazorExt`**; old ID published once as deprecated |
| Default culture | **neutral English**, `it-IT` opt-in via `GKit.UI.Localization` (behaviour change — §6) |
| Radzen version | **11.4.1** (latest stable) |
| Validation strategy (rev 3) | Mud adapter keeps `MudForm`; no behavioural drift (§8) |
| CSS coexistence budget (rev 3) | Moot — no in-app mixing |

### Version facts verified against Radzen.Blazor 11.4.1

- **Native `net10.0` target** (alongside net8.0/net9.0) — no TFM friction with this repo.
- Depends on `Microsoft.AspNetCore.Components{,.Web} 10.0.12`. **Done:** the repo's pins were bumped from `10.0.11` to `10.0.12` across 33 references in 19 projects. Solution restores and builds clean (0 errors; the 25 warnings and 8 integration-test failures are pre-existing, verified against the pristine tree). Non-framework pins — `coverlet.collector 10.0.1`, `Serilog.*` — were deliberately left alone.
- Ships built-in `it` localization (§6) — no extra translation package needed on the Radzen side.
