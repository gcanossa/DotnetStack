# GKit.UI.Data

The UI-neutral engine behind GKit's entity grids: EF Core paging, DbContext lifecycle, CRUD
orchestration, XLSX export and ordering. References no component library, so it can be unit
tested without a renderer.

Applications normally get this transitively via `GKit.UI.MudBlazorExt` or `GKit.UI.RadzenExt`.

## EntityGridEngine

Owns the DbContext lifecycle around every grid operation and loads pages.

```cs
var engine = new EntityGridEngine<Company>
{
  ContextFactory = () => factory.CreateDbContext(),
  SharedContext  = ctx,              // optional
  QueryFactory   = c => c.Set<Company>().Include(p => p.Nation)
};

var page = await engine.LoadAsync(gridQuery, token);
```

Two modes:

- **Shared context** — every operation reuses it, serialised behind a lock, because a DbContext is
  not thread-safe and grid loads overlap with edits. The change tracker is cleared per load.
- **Per-operation context** — a fresh context each time, with entities attached explicitly and the
  query run untracked.

Cancellation is treated as routine: a virtualised grid abandons loads as the user scrolls or
filters, so both `TaskCanceledException` and provider-level aborts resolve to an empty page. Other
database errors propagate.

## EntityCrudOrchestrator

Create / edit / delete, including the confirmation prompt and success/failure notifications.
Hooks (`OnBeforeEdit`, `OnAfterNew`, …) let a component intercept each step.

## EditEntityDialogEngine

The submit flow: `OnBeforeValidationAsync` → validate → `OnAfterValidationAsync` →
`OnBeforeSubmitAsync` → close. Works against `IUiFormHandle`, so MudBlazor's MudForm and Radzen's
EditContext both drive the same sequence.

## EntityLookupEngine

Search, value/item projection and display text for autocompletes. A failed search notifies and
returns empty rather than faulting the form, since lookups fire per keystroke.

## QueryOrderExtensions

```cs
query.OrderBy(sorts);              // dotted paths: "Nation.Name"
query.NullCheckingOrderBy(sorts);  // guards every intermediate step against null
```

`NullCheckingOrderBy` turns `"A.B.C"` into
`x => x.A == null ? default : (x.A.B == null ? default : x.A.B.C)`, so sorting on a nullable
navigation does not throw.

## XlsExportExtensions

```cs
await query.ToXlsAsync(title, columns, stream);   // columns: IEnumerable<ExportColumn>
```

Adapters project their rendered columns onto `ExportColumn(Title, PropertyPath)`, skipping
template columns with no underlying property.
