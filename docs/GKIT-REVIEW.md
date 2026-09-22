# GKit.* — Review, fixes and improvements

Scope: all 20 `GKit.*` packages in this repo, reviewed against how they are actually consumed by
`AllGlass/ErpPalletLink`, `BFer/Benati.Rifiuti`, and `Stuff/StuffHR`.

`FabbriFC` and `Millesrl` do **not** consume the packages. `Millesrl/EventManager` contains the
*ancestor* local copies of `TelegramHost`, `TelegramBotEndpoint` and `SmtpHost` — useful as a
divergence reference, not as a consumer.

Severity legend:

| | meaning |
|---|---|
| **S1** | security issue, data loss/corruption, or production outage |
| **S2** | wrong behaviour a consumer will hit in normal use |
| **S3** | latent bug, resource leak, or API/design defect |
| **S4** | hygiene, consistency, packaging |

---

## 0. Cross-cutting issues

### 0.1 — S1 — Culture-sensitive parsing and formatting everywhere

These are Italian-locale deployments (`it-IT`: decimal comma, `,` as separator). Several packages
round-trip values through `ToString()` / `Convert.ChangeType()` **without an `IFormatProvider`**,
which uses `CurrentCulture`:

| Location | Code | Verified |
|---|---|---|
| `GKit.Settings/DbSettingsManager.cs:264` | `Convert.ChangeType(item.Value, prop.PropertyType)` | ✅ `Decimal_values_round_trip_regardless_of_the_ambient_culture` |
| `GKit.Settings/DbSettingsManager.cs:285` | `prop.GetValue(savingOptions)?.ToString()` | ✅ (same test) |
| `GKit.Reporting/CsvReporter.cs:64` | `$"\"{p.SelectValue(row)}\""` | ✅ `Numbers_are_written_with_an_invariant_decimal_separator` |
| `GKit.Reporting/XlsReporter.cs:561` | `col.SelectValue(datum)?.ToString()` | ✅ `Numbers_do_not_depend_on_the_ambient_culture` |
| `GKit.DbMigration/FileSystemMigrationMappingsStore.cs:2498` | `Convert.ChangeType(parts[0], types.Item1)` | — |
| `GKit.RENTRI/BaseClient.cs` | `Convert.ToInt32(...)` on paging headers | — |

A `decimal` setting saved as `"1,5"` under `it-IT` and read back on a machine running `en-US`
silently becomes `15`. **Fix:** thread `CultureInfo.InvariantCulture` through every persistence
path. Persistence is machine-to-machine, never human-facing.

`TimeSpan.Parse` / `TimeSpan.ToString()` are *not* affected — the round trip was tested and passes
(`TimeSpan_values_round_trip_regardless_of_the_ambient_culture`), because the default `"c"` format
is culture-invariant. Only the numeric and date types need fixing.

### 0.2 — S2 — `Convert.ChangeType` cannot handle the types actually used

`Convert.ChangeType` throws for `Nullable<T>`, `Guid`, `Uri`, and does not parse enums by name.
`GKit.Settings` and `GKit.DbMigration` both rely on it. **Fix:** use
`TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value)`, with an explicit
`Nullable.GetUnderlyingType` unwrap first.

### 0.3 — S2 — Reflection property copying does not filter writable properties

`RevisionExtensions.CopyToObject`, `DbSettingsManager.GetOptionsAsync` and
`ReflectionPdfStamper` all iterate `typeof(T).GetProperties()` and call `SetValue`
unconditionally. Any computed/get-only property (`public string FullName => ...`) throws
`ArgumentException: Property set method not found`. Indexers throw
`TargetParameterCountException`. **Fix:** filter with
`p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0`.

### 0.4 — S2 — Sync-over-async on hot/startup paths

`.ConfigureAwait(false).GetAwaiter().GetResult()` appears in:

- `GKit.Application/Application.cs:50` (runner execution)
- `GKit.Application/ApplicationExtensions.cs:156` (`ApplyPendingMigrations`)
- `GKit.Quartz/GKitQuartzExtensions.cs:751, 825, 841` (inside `ApplicationStarted`)
- `GKit.PLC/GKitPlcExtensions.cs:38`, `GKit.OpcUa/GKitOpcUaExtensions.cs` (options build)
- `GKit.PLC/PlcContext.cs:319` (`Dispose` → `DisposeAsync`)
- `GKit.RENTRI/BaseClient.cs` (`content.ReadAsByteArrayAsync()` while signing every request)

`BaseClient` is the dangerous one: it blocks a thread pool thread **per outbound RENTRI request**
inside a Blazor Server app. Under load this is a thread pool starvation vector.
**Fix:** make `AddAuthToHttpRequestMessage` / `OnPrepareRequest` async, and offer
`Application.WrapAsync`.

### 0.5 — S3 — No package validation, no changelog, silent breaking changes

`AddSmartCardHostCheck("SMART_CARD")` is called at `Stuff/StuffHR/StuffHR/Program.cs:95` against
`GKit.SmartCardHost` 0.0.9, but **no longer exists anywhere in this source tree**. It was removed
without notice. Only 5 of 20 projects set `<EnablePackageValidation>`.

**Fix:**
- Move common packaging metadata into `Directory.Build.props` and set
  `EnablePackageValidation`, `GenerateDocumentationFile`, `IsPackable` for *all* packages.
- Add `PackageValidationBaselineVersion` so removals fail the build.
- Add a `CHANGELOG.md` per package, or at minimum one at the repo root.

### 0.6 — S4 — Stray/incorrect dependencies

- `using NPOI.SS.Formula.Functions;` in `GKit.Reporting/ColumnDescriptor.cs`,
  `GroupingExtensions.cs`, `XlsExtensions.cs`, `XlsGroupingReporter.cs`, and
  `GKit.MudBlazorExt/GridQueryDataExtensions.cs`. Remove.
- `GKit.DbMigration` hard-references `Microsoft.EntityFrameworkCore.SqlServer`, forcing the
  SQL Server provider onto every consumer. It only needs `EntityFrameworkCore.Relational`.
- `GKit.Reporting` and `GKit.MudBlazorExt` reference `System.Security.Cryptography.Xml` for no
  visible reason.
- `GKit.RENTRI` pulls `Newtonsoft.Json` (NSwag stubs) into apps that otherwise use
  `System.Text.Json`. Regenerate the stubs with `System.Text.Json`.
- `Directory.Build.props` pins Nerdbank.GitVersioning 3.7.115 while every csproj overrides it
  to 3.10.85. Delete the per-project overrides and bump the shared one.

---

## 1. GKit.Quartz

Consumed by `AllGlass/ErpPalletLink` and `Stuff/StuffHR`, both using `[CronSchedule]` +
`GKit:Quartz:DisabledJobs`.

### 1.1 — S1 — `QuartzProbeJob` is always scheduled, but `QuartzProbe` is only registered by `AddQuartzCheck`

`UseGKitQuartz` unconditionally calls `ScheduleQuartzCheck` (`GKitQuartzExtensions.cs:839`),
which schedules `QuartzProbeJob`. `QuartzProbe` is registered only inside
`AddQuartzCheck` (`QuartzHealthCheck.cs:931`). A consumer that calls `AddGKitQuartz()` +
`UseGKitQuartz()` **without** `AddQuartzCheck` gets an unresolvable-dependency exception from
the Quartz job factory on every heartbeat, forever.

Both current consumers happen to call `AddQuartzCheck`, so this is latent for them — but it is a
guaranteed failure for any new consumer.

**Fix:** register `QuartzProbe` in `AddGKitQuartz`, and bind `QuartzHealthCheckOptions` from
`GKit:Quartz:HealthCheck`. Only schedule the probe job when the probe is registered.

### 1.2 — S1 — `TimeSpanScheduleAttribute` does not mean an interval

```csharp
// GKitQuartzExtensions.cs:865
private static ITrigger ConfigureTimespanTrigger(IJobDetail job, TimeSpan time, int index) =>
    ... CronScheduleBuilder.DailyAtHourAndMinute(time.Hours, time.Minutes) ...
```

`[TimeSpanSchedule(TimeSpan.FromMinutes(15))]` does **not** run every 15 minutes — it runs once a
day at 00:15. The option validator calls the field `Interval` and rejects having both `Interval`
and `CronExpression`, which reinforces the wrong reading. The type silently produces a schedule
1/96th as frequent as intended.

**Fix:** decide and make it explicit. Either rename to `DailyAtScheduleAttribute` /
`ScheduleOptions.DailyAt`, or implement it as
`SimpleScheduleBuilder.Create().WithInterval(time).RepeatForever()`. I recommend *both*:
`[DailyAtSchedule]` and `[IntervalSchedule]`, with `ScheduleOptions.DailyAt` and
`ScheduleOptions.Interval`.

### 1.3 — S2 — `DisabledJobs` is ignored whenever `EnabledJobs` is set

```csharp
// GKitQuartzExtensions.cs:776
Enabled = options.Value?.EnabledJobs?.Any(p => p == type.FullName) ?? true &&
    !(options.Value?.DisabledJobs?.Any(p => p == type.FullName) ?? false)
```

`&&` binds tighter than `??`, so this parses as
`EnabledJobs?.Any(...) ?? (true && !DisabledJobs.Contains(...))`. With `EnabledJobs` non-null the
`DisabledJobs` term is dead. Both consumers only set `DisabledJobs`, so it currently works —
purely by luck.

**Fix:**

```csharp
var enabled = options.Value?.EnabledJobs is { Count: > 0 } allow
    ? allow.Contains(type.FullName)
    : options.Value?.DisabledJobs?.Contains(type.FullName) != true;
```

### 1.4 — S2 — Job discovery picks up abstract types and interfaces

`FindConfiguredJobTypes` (`:848`) filters only on `IsAssignableTo(typeof(IJob))`. That matches
`IJob` itself, abstract job base classes, and open generics. `JobBuilder.Create().OfType(...)`
then throws at startup. `assembly.GetTypes()` also throws `ReflectionTypeLoadException` on
assemblies with unresolvable references.

**Fix:**

```csharp
private static IEnumerable<Type> FindConfiguredJobTypes(Assembly assembly)
{
    Type[] types;
    try { types = assembly.GetTypes(); }
    catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t is not null).ToArray()!; }
    return types.Where(p => p.IsClass && !p.IsAbstract && !p.IsGenericTypeDefinition
                            && p.IsAssignableTo(typeof(IJob)));
}
```

### 1.5 — S3 — Job keys are regenerated on every start

`JobBuilder.Create().OfType(p.Type).WithIdentity($"{p.Type.Name}-{Guid.NewGuid():N}")` (`:792`).
With the default RAM job store this is harmless. With a persistent (ADO) job store it orphans a
row per job per restart, and `ScheduleJobs(..., replace: true)` will never match the old key.
Trigger keys, conversely, are *not* unique — `$"{job.JobType.Name}_{index}_trigger"` collides when
the same job type is scanned from two assemblies.

**Fix:** use a deterministic job key (`type.FullName`) and scope trigger keys to it.

### 1.6 — S3 — `NullReferenceException` in option validation

`.Validate(options => !options.Schedules.Any(p => string.IsNullOrEmpty(p.JobTypeName.Trim())))` —
`JobTypeName` is `required`, but configuration binding will happily leave it null.
Use `string.IsNullOrWhiteSpace(p.JobTypeName)`.

### 1.7 — S3 — `Assembly.GetEntryAssembly()!` and `GetTriggers` typing

`GetEntryAssembly()` returns null under a test host or when loaded from unmanaged code — three
`!`-suppressed dereferences. `GetTriggers` returns `IEnumerable<object>`; it should return
`IEnumerable<ITrigger>`.

---

## 2. GKit.EntityFramework

Consumed heavily by `BFer` (14 entity types implement `IRevisionableEntity` /
`ISoftDeletableEntity`).

### 2.1 — S1 — Interceptors hold mutable state and are shared across contexts

```csharp
// BFer/Benati.Rifiuti.Data/EF/BenatiDbContext.cs:13
public RevisionInterceptor RevisionInterceptor { get; private set; } = new RevisionInterceptor();
```

The interceptor is a field initializer on the `DbContext`, so BFer happens to get one per
context — but EF Core caches the *service provider* keyed by the options built in
`OnConfiguring`, and `AddInterceptors` on a pooled or factory-created context will share the
instance. `_updateRegistrations` / `_hardDeleteRegistrations` are plain `List<T>` mutated and
`Clear()`ed inside `SavingChanges` with no synchronisation.

Concrete failure: two Blazor circuits save concurrently against contexts from
`IDbContextFactory<T>`; circuit A calls `ctx.Remove(entity, hard: true)`, circuit B's
`SaveChanges` runs first and `Clear()`s the list, so A's entity is soft-deleted instead of hard
deleted — or worse, B's entity is hard-deleted.

**Fix:** stop storing registrations on the interceptor. Track them per-`DbContext` using
`DbContext.ChangeTracker` annotations or a `ConditionalWeakTable<DbContext, HashSet<object>>`
keyed off `eventData.Context`, and clear only that context's set.

### 2.2 — S3 — `RevisionInterceptor` calls `entry.Reload()` inside `SavingChanges`

```csharp
// RevisionInterceptor.cs:578
entry.Reload();
entity.Revision.IsCurrent = false;
```

To be precise about what is and is not broken here: the *outcome* is correct. Tests
`Updating_a_revisionable_entity_creates_a_new_current_revision` and
`The_superseded_revision_is_persisted_as_not_current` both **pass** against SQLite — the new
revision is inserted, the old row is flagged `IsCurrent = false`, and the `DocumentId` is carried
across. I expected the post-`Reload()` mutation to escape change detection; it does not.

What remains is a mechanism concern, not a behavioural bug:

- `Reload()` issues a **synchronous query on the same connection while a save is in flight**. On
  SQL Server without MARS that throws; inside an explicit transaction it can deadlock. BFer runs
  on SQL Server, so this is worth changing even though SQLite is happy.
- `SavingChangesAsync` (`:604`) just wraps the sync path, so `SaveChangesAsync` blocks a thread
  pool thread per revisioned entity.
- It is a full database round trip per modified entity, on every save.

**Fix:** implement `SavingChangesAsync` properly with `await entry.ReloadAsync(cancellationToken)`,
or avoid the round trip entirely with
`entry.CurrentValues.SetValues(entry.OriginalValues)`.

### 2.3a — S2 — `exclude:` silently does nothing for value-type properties

Verified by `CopyToObject_honours_the_exclude_expression` (expected `0`, got `1`).

```csharp
// RevisionExtensions.cs:419
if (exclude.Body is NewExpression newExp) { ... }
else if (exclude.Body is MemberExpression memberExp) { ... }
```

`Expression<Func<T, object>>` boxes value types, so `p => p.Id` for an `int Id` compiles to
`Convert(p.Id, Object)` — a `UnaryExpression`, not a `MemberExpression`. Neither branch matches,
the exclusion list stays empty, and the property is copied anyway. **Every `exclude:` on an
`int`/`decimal`/`DateTime`/`bool`/`Guid` property is silently ignored**; only `string` and other
reference-typed properties work.

This is exactly the shape used to exclude primary keys (`CopyTo(to, exclude: p => p.Id)`), so a
copy intended to preserve the target's identity overwrites it instead.

**Fix:** unwrap the conversion before matching:

```csharp
static Expression Unwrap(Expression e) =>
    e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u
        ? u.Operand : e;
```

and apply it to `exclude.Body` and to each member of a `NewExpression`.

### 2.3 — S2 — `CopyFromObject` silently drops `excludeProps`

```csharp
// RevisionExtensions.cs:448
public static T CopyFromObject<T>(this T to, T from, Expression<Func<T,object>>? exclude = null,
                                  IEnumerable<string>? excludeProps = null) where T : class
{
    return from.CopyToObject(to, exclude);   // <-- excludeProps never forwarded
}
```

Every property named in `excludeProps` is copied anyway. **Fix:** forward the argument.

### 2.4 — S2 — Missing query helpers force consumers to hand-roll filters

`BFer/Benati.Rifiuti.Data/EF/ModelExtensions.cs` defines:

```csharp
public static IQueryable<T> WithoutNotActive<T>(this IQueryable<T> ext) where T : IRevisionableEntity
  => ext.Where(p => p.Revision.IsCurrent);
public static IQueryable<T> WithoutDeleted<T>(this IQueryable<T> ext) where T : ISoftDeletableEntity
  => ext.Where(p => p.DeletedAt == null);
public static IQueryable<WasteMovement> IgnoreDependantQueryFilters(this IQueryable<WasteMovement> ext)
  => ext.IgnoreQueryFilters().WithoutDeleted();
```

`.WithoutNotActive()` is then repeated on ~12 query sites. `WithSoftDelete()` installs a query
filter but there is no `WithCurrentRevision()` equivalent, and `IgnoreDependantQueryFilters` only
exists because `EntityGrid` unconditionally strips filters (see 4.1).

**Fix:** ship in `GKit.EntityFramework`:

```csharp
public static EntityTypeBuilder<T> WithCurrentRevisionFilter<T>(this EntityTypeBuilder<T> b)
    where T : class, IRevisionableEntity
    => b.HasQueryFilter(p => p.Revision.IsCurrent);

public static IQueryable<T> OnlyCurrent<T>(this IQueryable<T> q) where T : IRevisionableEntity
    => q.Where(p => p.Revision.IsCurrent);

public static IQueryable<T> OnlyAlive<T>(this IQueryable<T> q) where T : ISoftDeletableEntity
    => q.Where(p => p.DeletedAt == null);
```

EF Core 10 supports *named* query filters, which lets a consumer disable soft-delete while
keeping the revision filter — exactly what `IgnoreDependantQueryFilters` is working around.

### 2.5 — S3 — Local time, and `DateTime.Now` for audit stamps

`RevisionInfo.NewRevision()` / `First()` and `SoftDeleteInterceptor` all use `DateTime.Now`.
Audit columns should be `DateTimeOffset.UtcNow` (or at minimum `UtcNow`); the current values are
ambiguous across DST transitions.

### 2.6 — S3 — `DisableSoftDelete(entity, hard)` has an unused parameter

`SoftDeleteExtensions.cs:616` takes `bool hard = false` and never reads it. Remove it.

### 2.7 — S3 — `AdjustNewRevisionReferences` reflects over indexers

`RevisionInterceptor.cs:594` iterates `GetProperties()` and calls `GetValue(entry.Entity)`. An
entity with an indexer throws `TargetParameterCountException`. Also uses `==` reference equality,
which is correct here but fragile — prefer `ReferenceEquals` for intent.

### 2.8 — S3 — `RevisionInfo.DocumentId` allocates a CUID on every EF materialisation

`public string DocumentId { get; set; } = new Cuid2().ToString();` runs for every row loaded, then
is immediately overwritten by the value from the database. On a 50k-row export that is 50k wasted
CUID generations. Move the default into `First()`.

---

## 3. GKit.Settings

### 3.1 — S2 — Key prefix matching collides

```csharp
// DbSettingsManager.cs:247
.Where(p => p.Key.StartsWith(BaseKey))
```

`BaseKey` is `typeof(TOptions).Name`. `SmtpHostOptions` and `SmtpHostOptionsV2` both match the
first, so settings bleed between option types. **Fix:** `StartsWith(BaseKey + ":")`.

### 3.2 — S2 — `Setting` has no key configuration and no DI registration

`Setting` has properties `Key`, `Value`, `TypeName` — none of which EF recognises as a primary
key by convention (`Key` is not `Id`/`SettingId`). Any consumer calling `context.Set<Setting>()`
gets a model-building error until they hand-write `HasKey`. There is also no
`AddGKitSettings<TOptions, TContext>()` extension, so wiring is entirely manual.

**Fix:**

```csharp
public static ModelBuilder AddGKitSettings(this ModelBuilder b)
{
    b.Entity<Setting>(e => { e.HasKey(p => p.Key); e.Property(p => p.Key).HasMaxLength(256); });
    return b;
}

public static IServiceCollection AddDbSettings<TOptions, TContext>(this IServiceCollection s, string section)
    where TOptions : class, new() where TContext : DbContext
{
    s.AddOptions<TOptions>().BindConfiguration(section);
    s.AddSingleton<SettingsManager<TOptions>, DbSettingsManager<TOptions, TContext>>();
    return s;
}
```

### 3.3 — S2 — `TypeName` is written but never read

`SaveOptionsAsync` stores `prop.PropertyType.AssemblyQualifiedName`, and `GetOptionsAsync`
ignores it entirely, re-deriving the type from the CLR property. Either use it (to detect
schema drift and fail loudly) or drop the column.

### 3.4 — S3 — `IOptions<TOptions>` never reflects updates

`DbSettingsManager` overlays DB values on top of `IOptions<TOptions>.Value`, which is a
process-lifetime snapshot. After `UpdateOptionsAsync`, anything injecting `IOptions<TOptions>`
directly (rather than `SettingsManager<TOptions>`) still sees the old values.
`GKit.SmtpHost/SmtpHostService` works around this by subscribing to `OptionsChanged` and bouncing
the server. **Fix:** implement `IOptionsChangeTokenSource<TOptions>` so
`IOptionsMonitor<TOptions>` works, and register the manager as the source of truth.

### 3.5 — S3 — `OptionsChanged` handlers are invoked unguarded

`SettingsManager.UpdateOptionsAsync` awaits `OptionsChanged.Invoke(options)` — a multicast
`Func<T,Task>` only awaits the *last* handler, and one throwing handler aborts the update. **Fix:**

```csharp
if (OptionsChanged is not null)
    await Task.WhenAll(OptionsChanged.GetInvocationList()
        .Cast<Func<TOptions, Task>>().Select(h => h(options)));
```

### 3.6 — S3 — Asymmetric property selection

`SaveOptionsAsync` uses the overridable `SelectProperties()`; `GetOptionsAsync` uses
`typeof(TOptions).GetProperties()` directly. An override that narrows the set writes a subset but
reads the superset.

---

## 4. GKit.MudBlazorExt

21 `.razor` files across BFer and StuffHR use `EntityGrid` / `ManagedGrid`.

### 4.1 — S1 — `EntityGrid` strips all query filters, exposing soft-deleted rows

```csharp
// EntityGrid.razor.cs:1226
var query = QueryFactory?.Invoke(ctx)?.IgnoreQueryFilters() ?? throw ...
```

`GKit.EntityFramework.WithSoftDelete()` implements soft delete *as a query filter*. Every
`EntityGrid` therefore shows deleted rows and superseded revisions. This is why BFer's grid
`QueryFactory` delegates all repeat `.WithoutNotActive()`, and why
`IgnoreDependantQueryFilters()` exists.

Worse, `ExportXlsAsync` (`:1205`) does **not** call `IgnoreQueryFilters`, so the exported XLSX has
a different row set than the grid the user is looking at.

**Fix:** make it a parameter, default off:

```csharp
[Parameter] public bool IgnoreQueryFilters { get; set; }
...
var query = QueryFactory.Invoke(ctx);
if (IgnoreQueryFilters) query = query.IgnoreQueryFilters();
```

and apply the same flag in `ExportXlsAsync`.

### 4.2 — S2 — `AsNoTracking()` result is discarded

```csharp
// EntityGrid.razor.cs:1228
if (!IsSharedContext())
  query.AsNoTracking();     // return value thrown away — no effect
```

`AsNoTracking` is a pure function. Every non-shared-context grid page tracks every row it loads;
with `Virtualize` scrolling through 10k rows the change tracker grows without bound and
subsequent `SaveChanges` calls get slower and slower.

**Fix:** `query = query.AsNoTracking();`

### 4.3 — S2 — `OnLoadedServerData` fires twice per load

`EntityGrid.LoadEntityData` has `finally { await OnLoadedServerData.InvokeAsync(gridState); }`
(`:1265`), and it is invoked *through* `ManagedGrid.ManagedLoadServerData`, which has the same
`finally` (`:1841`). Any consumer counting loads or toggling a spinner in that callback sees
double events.

**Fix:** remove the duplicate from `EntityGrid.LoadEntityData`; the base class already owns it.
The `TaskCanceledException` / `DbException` handling is duplicated in the same way.

### 4.4 — S2 — `ToXlsAsync` NREs on template/computed columns and produces a mislabelled file

```csharp
// MudDataGridExtensions.cs:2000
var properties = columns.ToDictionary(p => p, p => p.PropertyName.Split(".")...
    acc.Add(typeof(T).GetProperty(cur)!);
```

Only `TemplateColumn<T>` is filtered out. A `PropertyColumn` bound to an expression that is not a
plain property chain (e.g. `x => x.Lines.Count`) has a `PropertyName` that `GetProperty` returns
null for — the `!` then NREs inside `Aggregate`. The file is also written as `.xls` while the
content is XSSF/OOXML (`.xlsx`); Excel shows a format-mismatch warning.

**Fix:** skip columns where resolution fails (log and emit an empty column), and rename the
download to `.xlsx`.

### 4.5 — S2 — `EditEntityDialog` cannot save a pre-populated model

```razor
<MudButton OnClick="SubmitAsync" Disabled="!(_form?.IsTouched ?? false)">Salva</MudButton>
```

`EntityGrid.NewAsync` supports `NewValueFactory` to build a pre-filled entity. If the user accepts
the defaults without editing a field, `IsTouched` is false and Save stays disabled.
**Fix:** `Disabled="_saving"`, and rely on validation for correctness.

### 4.6 — S3 — JS interop runs on every render

`EditEntityDialog.OnAfterRenderAsync` calls `setter.SetAttributes(...)` unconditionally rather
than `if (firstRender)`. Every keystroke that triggers a re-render makes a JS interop round trip.

### 4.7 — S3 — `WithDbContextPreamble` retries an identical failing call

```csharp
try { ctx.AttachRange(entities); }
catch (InvalidOperationException) { ctx.AttachRange(entities); }
```

The retry has identical inputs and will throw identically. Either handle the real cause (an entity
with the same key already tracked → use `ctx.ChangeTracker.Clear()` or `Entry(e).State`) or let it
propagate.

### 4.8 — S3 — `_ctxLock` semaphore can be released without being taken, and is never disposed

`WithDbContextPreamble` acquires the semaphore *after* `AttachRange`; `WithDbContextEpilogue`
releases whenever `ctx != null && shared`. If `AttachRange` throws in shared mode the epilogue
releases a semaphore that was never taken → `SemaphoreFullException`. `EntityGrid` is also
`IDisposable`-less, so `_ctxLock` leaks per grid instance.

### 4.9 — S3 — Swallowed exception variables and lost stack traces

`logger.LogError(e.Message)` (`:1102`, `:1137`) logs a *string* as the message template and
discards the exception. `EntityItemAutocomplete.NewAsync` catches `Exception e` and never uses it.
Use `logger.LogError(e, "Unable to delete {Entity}", typeof(T).Name)`.

### 4.10 — S3 — Parameter shadowing with `protected new`

`EntityGrid`, `EntityAutocomplete` and `ManagedAutocomplete` hide inherited `[Parameter]`
properties with `protected new` members. Blazor's parameter binder walks the most-derived type;
this pattern is undefined behaviour in practice and breaks when the base changes.
**Fix:** restructure so the base type does not expose parameters the derived type must hide — e.g.
make `LoadServerData` a `protected virtual` method rather than a `[Parameter]`.

### 4.11 — S3 — `DataAutocomplete` blocks on EF queries

`DataAutocomplete.Search` calls `query.ToList()` (sync) and ignores the `CancellationToken`.
`EntityItemAutocomplete.Search` does this correctly with `ToListAsync(token)` — make them
consistent.

### 4.12 — S3 — `NullCheckingOrderBy` builds a type-unsafe conditional

`GridQueryDataExtensions.cs:1508` builds `Expression.Condition(..., Expression.Default(property.PropertyType), result)`
where `property` is the *last* resolved property, not necessarily the branch type. When the
branches differ, `Expression.Condition` throws `ArgumentException` at query-translation time.

---

## 5. GKit.BlazorExt

### 5.1 — S2 — `DocumentEventSourceBase.DisposeAsync` cannot disconnect the listener

```csharp
await _module.InvokeVoidAsync("disconnect", this);
```

`connect` was given a `DotNetObjectReference`; `disconnect` is given the raw object, which the JS
interop layer serialises as JSON. The JS side cannot match it to the registered listener, so the
`document` event handler is never removed — and the exception is swallowed by `catch { }`.
The `DotNetObjectReference` created in `Connect` (`DocumentEventServiceBase.cs:310`) is also never
disposed.

**Fix:** keep the `DotNetObjectReference` on the source, pass it to `disconnect`, and dispose it.
Every navigation in a Blazor Server app currently leaks a listener plus a .NET object reference.

### 5.2 — S2 — `DbContextProvider` drops the cascading value

`DbContextProvider.razor` inherits `DbContextFactoryProvider<T>` but replaces the markup with bare
`@ChildContent`, discarding the base's `<CascadingValue Value="this">`. A child declaring
`[CascadingParameter] DbContextProvider<T> Provider` never receives it.

`Context` is also null until `OnInitializedAsync` completes, so the first render of any child that
dereferences it NREs.

**Fix:** re-emit the cascading value and render children only once `Context is not null`.

### 5.3 — S2 — `TimerService` loses async exceptions and overflows

```csharp
public IDisposable SetIntervalAsync(Func<Task> callback, TimeSpan timeout)
    => new Timer(p => callback?.Invoke(), null, (int)timeout.TotalMilliseconds, (int)timeout.TotalMilliseconds);
```

The returned `Task` is discarded — a faulting callback becomes an unobserved exception, silently
stopping nothing but reporting nothing either. The `(int)` casts overflow for any period above
~24.8 days. **Fix:** use the `TimeSpan` overload of `Timer` and `async void`-free error handling:

```csharp
new Timer(async _ => { try { await callback(); } catch (Exception e) { logger.LogError(e, "..."); } },
          null, period, period);
```

Also: `TimerService` is registered `Scoped`, but the `Timer` it hands back outlives the scope
unless the caller disposes it. `TimedScope` does this correctly; document the contract.

### 5.4 — S3 — JS-interop services are unusable during prerendering

Every service eagerly builds `moduleTask` from `IJSRuntime`. During SSR prerendering
`InvokeAsync("import", ...)` throws. Guard with `OperatingSystem.IsBrowser()` /
`IJSRuntime is IJSInProcessRuntime`, or document "only call from `OnAfterRenderAsync`".

### 5.5 — S3 — `CascadingStateValueSource` fires and forgets

`_ = NotifyChangedAsync();` — exceptions are unobserved and the notification does not run on the
renderer's synchronisation context. Prefer capturing the task and surfacing failures.

### 5.6 — S3 — `DisposableScope` overload ambiguity

`AddDisposable<T>(T obj)` vs `AddDisposable<T>(Func<T> factory)` are ambiguous for a `T` that is
itself a delegate; `RemoveDisposable(IDisposable)` / `RemoveDisposable(IAsyncDisposable)` are
ambiguous for any type implementing both (which includes `DbContext`). Rename the factory
overloads to `AddDisposableFrom`.

---

## 6. GKit.Application

Used by all three consumers as the `Program.cs` entry point.

### 6.1 — S1 — A fatal exception still exits with code 0

```csharp
catch (Exception ex) { Log.Fatal(ex, "Application terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }
```

The process exits 0. Docker `restart: on-failure`, systemd `Restart=on-failure`, Kubernetes
`CrashLoopBackOff` and CI all treat this as a clean shutdown. A container that fails to reach its
database on startup will sit "successfully exited" forever.

**Fix:** `Environment.ExitCode = 1;` in the catch (or return an `int` from `Wrap`).

### 6.2 — S2 — `ApplyPendingMigrations` silently does nothing in Development

```csharp
var environment = ext.Services.GetRequiredService<IHostEnvironment>();
if (environment.IsDevelopment()) return ext;
```

The name promises migration; in Development it is a no-op with no log line. Both BFer and StuffHR
call it in `Program.cs`. A developer running locally gets "table not found" with no explanation.

**Fix:** log the skip at Information, and add an `applyInDevelopment: false` parameter so the
behaviour is visible at the call site.

### 6.3 — S2 — The host is never disposed; `--help` leaks it

`var host = app();` is never disposed, and the `--help` branch `return`s early. `IHost` owns the
root service provider — every singleton `IDisposable` (DbContext factories, PLC/OPC UA sessions,
`SmtpServer`) is finalised at best. **Fix:** `using var host = app();`

### 6.4 — S3 — `ExecuteApplyPendingMigrations` disposes a service it does not own

```csharp
using var scope = ext.Services.CreateScope();
await using var ctx = scope.ServiceProvider.GetRequiredService<T>();
```

The scope owns the context; disposing it explicitly double-disposes when the scope is disposed.
`DbContext.Dispose` is idempotent so nothing breaks today, but the ownership is wrong. Drop the
`await using` (or use `IDbContextFactory<T>` and own it properly).

### 6.5 — S3 — `--help` only works as the sole argument

`if (args.Length == 1 && args[0] == "--help")`. `myapp --apply-migrations --help` runs the
migration. Use `args.Contains("--help")`.

### 6.6 — S3 — `MapHealthChecksJson` buffers twice and ignores cancellation

The writer builds a `MemoryStream`, converts it to a `string` via `Encoding.UTF8.GetString`, then
writes the string. Write directly to `context.Response.Body` and pass
`context.RequestAborted` to `WriteAsync`.

---

## 7. GKit.RENTRI

Used by `BFer/Benati.Rifiuti.WebApp`, which creates a client per lookup call across ~15 methods in
`Rentri/DataLookupSource.cs`.

### 7.1 — S1 — A new `HttpClient` and `HttpClientHandler` per call → socket exhaustion

```csharp
// Factories/RentriHttpClientFactory.cs
public static HttpClient Create() => new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
```

`DataLookupSource` does `using var client = _codificheFactory.CreateClient(...)` in every lookup
method. Each creates a fresh handler and connection pool; disposal leaves the socket in
`TIME_WAIT`. This is the textbook `HttpClient` anti-pattern, and the package already references
`Microsoft.Extensions.Http` without using it.

**Fix:** register named/typed clients via `IHttpClientFactory`:

```csharp
services.AddHttpClient(nameof(CodificheClient))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));
```

and make `BaseClient.Dispose` a no-op for factory-owned clients.

### 7.2 — S1 — `CreateAnonymousClient()` always talks to production

```csharp
public T CreateAnonymousClient() => CreateClient(null!);
```

`Options` is null, so `AnagraficheClient`'s constructor skips the `BaseUrl` rewrite and keeps the
stub's baked-in `https://api.rentri.gov.it`. `ApiStatusService` uses **only** anonymous clients —
so a demo-configured deployment reports the health of the *live* RENTRI environment, and
`RentriHealthCheck` reports green/red for an endpoint the app never calls.

**Fix:** give `CreateAnonymousClient` an environment parameter (or an `ApiEnvironment` enum on the
factory) and always rewrite `BaseUrl`.

### 7.3 — S1 — A 401 from RENTRI kills the whole host

```csharp
catch (ApiException e) { logger.LogError(...); ...; throw; }
```

Rethrowing out of `BackgroundService.ExecuteAsync` triggers the .NET 6+ default
`BackgroundServiceExceptionBehavior.StopHost`. An expired RENTRI certificate takes the entire
Blazor application down instead of degrading the health check.

**Fix:** never rethrow; set all statuses to `Unauthorized`, log, and keep polling with backoff.

### 7.4 — S1 — `ApiResultsCache` is not thread-safe

`_cache` and `_cacheSemaphores` are plain `Dictionary<,>`. The fast path
(`_cache.TryGetValue`) reads while another thread writes `_cache[cacheKey] = value`, and
`GetCacheSemaphore` reads `_cacheSemaphores[cacheKey]` outside the lock.
Concurrent `Dictionary` mutation corrupts buckets and can hang in an infinite loop inside
`FindEntry`. `DataLookupSource` calls this from Blazor circuits, i.e. concurrently by construction.

**Fix:** use `ConcurrentDictionary` for both, or replace the whole class with
`IMemoryCache` + `HybridCache`. Also use `DateTimeOffset.UtcNow` for expiry.

### 7.5 — S2 — `Retry-After` is parsed as a `TimeSpan`

```csharp
CurrentContext.RetryAfter = TimeSpan.Parse(response.Headers.GetValues("Retry-After").First());
```

RFC 9110 `Retry-After` is either delta-seconds (`"120"`) or an HTTP-date.
`TimeSpan.Parse("120")` yields **120 days**. Any retry logic honouring this value stalls forever.

**Fix:** use `response.Headers.RetryAfter` (`RetryConditionHeaderValue`), which models both forms.

### 7.6 — S2 — `UseContext()` blocks synchronously

`_contextSemaphore.Wait()` inside `UseContext`, called from `WithContext`. Blocks a thread pool
thread for the duration of a remote HTTP call. **Fix:** `await _contextSemaphore.WaitAsync(ct)` and
make `UseContext` async. `private readonly Lock _lock = new();` is dead — delete it.

### 7.7 — S2 — JWT lifetime is implicit

`CreateBaseTokenDescriptor` sets no `IssuedAt` / `NotBefore` / `Expires`, so
`JsonWebTokenHandler` applies its 60-minute default. RENTRI's `Agid-JWT-Signature` profile expects
short-lived tokens with an explicit `iat`. Set them explicitly (e.g. `Expires = UtcNow.AddMinutes(5)`).

### 7.8 — S2 — Contradictory certificate storage flags

```csharp
X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet
```

These are mutually exclusive in intent: `MachineKeySet` persists to the machine store (requires
elevation on Windows), `EphemeralKeySet` keeps the key in memory and is unsupported for some
operations on Windows. Use `EphemeralKeySet` alone on Linux; drop `MachineKeySet`.
The loaded `X509Certificate2` is also never disposed — `ClientOptions` should implement
`IDisposable`.

### 7.9 — S3 — Status routing by reflection over type names

```csharp
var prop = typeof(ApiStatusProvider).GetProperty(typeof(T).Name.Replace("Client", string.Empty))!;
prop.SetValue(apiStatusProvider, status);
```

Renaming `CaRentriClient` or `ApiStatusProvider.CaRentri` produces a null-deref at runtime with no
compile error. **Fix:** add `protected abstract RentriApi Api { get; }` to `BaseClientFactory<T>`
and switch on the enum.

### 7.10 — S3 — Assorted

- `PrepareRequestHandler` is assigned an empty lambda — dead code.
- `public event Action<ApiStatus> StatusChanged;` is non-nullable and never initialised (CS8618),
  and reports the new value without saying *which* API changed.
- `GetApiStatusFromHttpStatusCode`: `<= 400 => Available` classifies `400 Bad Request` as healthy.
- `CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)` is created per loop iteration
  and never disposed, and adds nothing (no timeout).
- `ApiResultsCache` is never registered in `AddRentriServices` — BFer news it up by hand.
- `IServiceCollectionExtensions.cs` declares `class RentriExtensions` — rename the file.
- The 5-minute poll interval is hard-coded with no way to configure or disable it.

---

## 8. GKit.OpcUa

Used by `AllGlass/ErpPalletLink`: `AddOpcUaContextFactory<PalletOpcUaContext>`,
`AddOpcUaContextCheck`, plus `CreateContextAsync()` from a job running
`[CronSchedule("0/20 * * ? * MON-SUN *")]` — every 20 seconds.

### 8.1 — S1 — Every `CreateContextAsync` tears down the shared session

```csharp
// OpcUaContextFactory.cs
var context = (T)Activator.CreateInstance(typeof(T), Options)!;
await context.RenewConnection(ct);       // -> CloseConnectionAsync + OpenConnectionAsync
```

`RenewConnection` → `CloseConnectionAsync` removes the connection from `OpcUaConnectionPool`,
calls `Session.CloseAsync` and disposes it, then `OpenConnectionAsync` builds a brand new session.
So the "pool" never pools: `PalletSynchronizeJob` destroys and recreates the OPC UA session every
20 seconds, and `OpcUaContextHealthCheck` does the same on every probe. Any subscription is lost
each time, session-establishment traffic dominates, and the server sees continuous
session churn (many servers cap concurrent/consecutive sessions).

**Fix:** `CreateContextAsync` should call `EnsureConnected`, not `RenewConnection`. Reserve
`RenewConnection` for explicit recovery.

### 8.2 — S1 — Direct (non-reverse) connections never reconnect

```csharp
// OpcUaConnection.cs, Session_KeepAlive
if (ServiceResult.IsBad(e.Status) && ReverseConnectManager is not null) { ...BeginReconnect... }
```

The reconnect path is gated on `ReverseConnectManager is not null`. AllGlass connects directly
(`ServerUrl`), so a dropped session is never recovered — it stays dead until the next
`CreateContextAsync` happens to rebuild it (which, given 8.1, masks the bug entirely; fixing 8.1
without fixing this would expose it).

**Fix:** use the `BeginReconnect(Session, reverseConnectManager: null, period, callback)` overload
when there is no reverse connect manager.

### 8.3 — S2 — `OpenConnectionAsync` leaks a connection when the pool already has one

```csharp
var connection = new OpcUaConnection(Options);
if (OpcUaConnectionPool.Connections.TryAdd(Options, connection))
    await connection.ConnectAsync(ct);
// else: `connection` is dropped on the floor, undisposed
```

Under a race, two threads each construct an `OpcUaConnection`; the loser's instance is never
disposed. **Fix:** `GetOrAdd` with a factory, and dispose the loser.

### 8.4 — S2 — Nothing ever closes pooled sessions

`OpcUaContext.Dispose()` deliberately does not close the connection (it is pooled), and the pool
is a `static` with no owner. On host shutdown, sessions are never closed cleanly — the server
holds them until its own session timeout expires. **Fix:** make the pool a registered singleton
implementing `IAsyncDisposable`, or register an `IHostedService` that drains it on stop.

### 8.5 — S2 — `ConnectAsync` swallows every exception

```csharp
catch (Exception ex) { Logger.LogWarning(ex, "Create Session Error"); return false; }
```

Callers get `false` with no reason; `EnsureConnected` turns it into a bare
`InvalidOperationException("Connection failed")`. Certificate rejection, bad URL and timeout are
indistinguishable. **Fix:** throw a typed exception, or return a result carrying the
`ServiceResult`.

### 8.6 — S3 — `AcceptUntrustedCertificates` is too narrow

`CertificateValidation` only accepts `BadCertificateUntrusted`. Field deployments routinely hit
`BadCertificateChainIncomplete`, `BadCertificateHostNameInvalid` and `BadCertificateTimeInvalid`.
Either widen the set explicitly or document the limitation.

### 8.7 — S3 — Mutable dictionary key

`OpcUaConnectionPool` is keyed on `IOpcUaContextOptions`, whose `ServerUrl` and `TelemetryContext`
have public setters. Mutating them after insertion (the interface permits it) makes the entry
unreachable. Key on an immutable record of the connection-defining fields.

---

## 9. GKit.PLC

Used by `AllGlass/ErpPalletLink` (`TermofarPlcContext`, `AddPlcContextCheck`, plus a 20-second job).

### 9.1 — S1 — CLR→S7 type mapping is wrong for signed integers

```csharp
// ModelBuilder.cs:62
if (clrType == typeof(short) ...) return VarType.Word;    // Word is UNSIGNED 16-bit
if (clrType == typeof(int)   ...) return VarType.DWord;   // DWord is UNSIGNED 32-bit
if (clrType == typeof(long)  ...) return VarType.DInt;    // DInt is SIGNED 32-bit
```

- `short` → `Word`: a PLC value of `-1` reads back as `65535`.
- `int` → `DWord`: `-1` reads back as `4294967295`.
- `long` → `DInt`: reads 4 bytes into a 64-bit field; the mapping is simply wrong.
- `bool` is not handled at all and falls through to the `VarType.DWord` default, so
  `.Property(p => p.Running)` silently reads a doubleword instead of a bit.

For a synchroniser writing production counters into an ERP, this is silent data corruption.

**Fix:**

```csharp
bool    => VarType.Bit,
byte    => VarType.Byte,
short   => VarType.Int,
ushort  => VarType.Word,
int     => VarType.DInt,
uint    => VarType.DWord,
float   => VarType.Real,
double  => VarType.LReal,
string  => VarType.String,
DateTime=> VarType.DateTimeLong,
_       => throw new NotSupportedException($"No S7 VarType for {clrType}")
```

Throwing beats a silent wrong default.

### 9.2 — S1 — A new TCP connection per `CreateContextAsync`, including every health check

`PlcContextFactory.CreateContextAsync` always calls `RenewConnection` → `OpenConnectionAsync` →
`new Plc(...)` + `OpenAsync`. `PlcContextHealthCheck` calls it on every probe, and
`TermofarSynchronizeJob` every 20 seconds. S7-300/400 CPUs support a small number of concurrent
PG/OP connections (often 2–4); this churn can exhaust them and lock out the engineering station.

**Fix:** mirror the OPC UA design properly — pool one `Plc` per options instance, reference-count
contexts, and have the health check inspect the pooled connection instead of opening a new one.

### 9.3 — S2 — `OpenConnectionAsync` orphans the previous `Plc`

```csharp
public async Task OpenConnectionAsync(CancellationToken ct = default)
{
    Connection = new Plc(...);   // previous Connection is never closed/disposed
    await Connection.OpenAsync(ct);
}
```

`EnsureConnected` has no locking, so two concurrent reads both see `IsConnected == false` and both
assign `Connection` — one socket is leaked per race. **Fix:** guard with a `SemaphoreSlim` and
close the old connection first.

### 9.4 — S2 — `Port` is a `short`

`IPlcContextOptions.Port` / `ApplyConnectionString`'s `short.Parse` cannot represent ports above
32767. Use `int` (or `ushort`).

### 9.5 — S3 — `Dispose()` blocks on `DisposeAsync()`

`PlcContext.Dispose() => DisposeAsync().GetAwaiter().GetResult()`, and `PlcContextHealthCheck`
uses `using var` (sync). Under ASP.NET's synchronisation context this is a deadlock shape.
Use `await using` in the health check and make the sync `Dispose` close the socket directly.

### 9.6 — S3 — `CloseConnectionAsync` is `async` with no `await`, and ignores `ct` (CS1998).

### 9.7 — S3 — `Activator.CreateInstance(typeof(T), Options)` is unchecked

A `PlcContext` subclass without the `(IPlcContextOptions)` constructor fails at runtime with
`MissingMethodException`. Add a `where T : PlcContext` + a documented constructor contract, or
accept a `Func<IPlcContextOptions<T>, T>` factory in `AddPlcContextFactory`.

---

## 10. GKit.SmtpHost

### 10.1 — S1 — The default identity store accepts every credential

```csharp
public class ConfigurationSmtpIdentityStore(IOptions<SmtpHostOptions> options) : ISmtpIdentityStore
{
    public Task<bool> CheckAsync(string username, string password, CancellationToken ct = default)
    {
        var result = true;          // <-- always
        return Task.FromResult(result);
    }
}
```

`AddSmtpHost` registers this as the default `ISmtpIdentityStore`, and `SmtpServerFactory` sets
`.AuthenticationRequired(true)` — which gives a false sense of security, because the authenticator
approves everything. With `AllowUnsecureAuthentication(true)` (the default when no PEM cert is
configured) the server accepts plaintext AUTH from anyone and queues their mail.

**Fix:** delete the default registration. `AddSmtpHost` should throw at startup if no
`ISmtpIdentityStore` was supplied via `WithIdentityStore<T>()`, or ship a
`ConfigurationSmtpIdentityStore` that actually reads credentials from `SmtpHostOptions`.

### 10.2 — S2 — `SmtpRouteAttribute` matches display names, not addresses

```csharp
message.From.Any(p => Regex.IsMatch(p.Name, FromPattern)) &&
message.To.Any(p => Regex.IsMatch(p.Name, ToPattern)) &&
Regex.IsMatch(message.Subject, SubjectPattern)
```

`InternetAddress.Name` is the *display name*, usually empty. `[SmtpRoute(fromPattern: "erp@.*")]`
therefore never matches. `Subject` may be null → `ArgumentNullException`. Patterns are also
recompiled per message with no timeout (ReDoS on attacker-controlled subjects).

**Fix:** match `MailboxAddress.Address`, coalesce `Subject` to `""`, and use a cached
`Regex` with `RegexOptions.Compiled | RegexOptions.CultureInvariant` and a `matchTimeout`.

### 10.3 — S2 — Controller discovery scans every loaded assembly per message

`ControllerRouteMessageHandler.SelectControllers` calls
`AppDomain.CurrentDomain.GetAssemblies().SelectMany(p => p.GetTypes())` **on every message**.
That is hundreds of thousands of reflection operations per mail. It also throws
`ReflectionTypeLoadException` on assemblies with unresolvable references.

**Fix:** build the route table once at startup (it is already built once in
`AddControllersWithRoutes`), cache it in a singleton, and match against the cached table.

### 10.4 — S2 — Dead-letter handling loses the message

`FileDeadLetterMessageHandler` serialises only `From`, `To`, `Subject` and `TextBody` to JSON.
HTML bodies and all attachments are discarded — the point of a dead letter is to be able to
reprocess it. Also `$"{DateTime.UtcNow.ToFileTimeUtc()}.json"` collides for messages arriving
within the same 100 ns tick.

**Fix:** `await message.WriteToAsync(Path.Combine(path, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml"))`.

### 10.5 — S3 — Restart loop and unawaited processing task

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    _server = _smtpServerFactory.Create(await _optionsManager.GetOptionsAsync());
    serverTask = _server.StartAsync(stoppingToken);
    await serverTask;
}
```

If `StartAsync` returns immediately (e.g. port already in use), this is a tight infinite loop
recreating the server with no delay and no logging. `processingTask` is started but only awaited
after the loop, so its exceptions are unobserved until shutdown.
**Fix:** add backoff + logging, and observe `processingTask` with `Task.WhenAny`.

### 10.6 — S3 — `DequeueAsync` ignores its `CancellationToken`

`SmtpHostBroker.DequeueAsync` calls `Reader.ReadAsync()` without the token (same bug in
`TelegramHostBroker`).

### 10.7 — S3 — `X509Certificate2.CreateFromPem` result is never disposed, and on Windows needs re-export

`X509Certificate2.CreateFromPem(...)` produces a certificate whose private key is not usable by
`SslStream` on Windows; the documented workaround is
`X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12), null)`.

---

## 11. GKit.TelegramHost / GKit.TelegramBotEndpoint

### 11.1 — S1 — `TelegramHostService.ExecuteAsync` is entirely commented out

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    //TODO: reenable TL.RpcException: 400 PHONE_NUMBER_BANNED
    // var processingTask = ProcessMessagesAsync(stoppingToken);
    // ...
}
```

`AddTelegramHost` registers a broker, controllers, a context provider, a verification-code manager
and a hosted service — and the hosted service does nothing. The package ships, resolves, starts,
and silently drops every message. `ProcessMessagesAsync` is dead code (CS0169-adjacent warnings
aside). No consumer uses it today.

**Fix:** either mark the package `<IsPackable>false</IsPackable>` / prerelease until it works, or
restore `ExecuteAsync` with a documented failure mode for `PHONE_NUMBER_BANNED` (log and stop the
*service*, not the host).

### 11.2 — S2 — `TelegramContextProvider` throws on the second `ClientReady()`

`_clientReady.SetResult(...)` throws `InvalidOperationException` if the TCS is already completed —
which happens on any reconnect. Use `TrySetResult` / `TrySetCanceled` throughout, and construct
the TCS with `TaskCreationOptions.RunContinuationsAsynchronously`.

### 11.3 — S2 — `TelegramConnection.Stop()` disposes before unsubscribing, then `SetResult`s unconditionally

```csharp
_client.Dispose();
_contextProvider.InvalidateClient();
_client.OnUpdates -= OnUpdate;    // after Dispose
_source.SetResult();              // throws if already completed (e.g. cancellation path)
```

`StartAsync` calls `_source.SetCanceled(...)` on cancellation, so a cancel followed by `Stop()`
throws. Reorder and use `TrySetResult`.

### 11.4 — S3 — Global mutable logger hook

`TelegramConnectionFactory`'s constructor assigns `WTelegram.Helpers.Log` — a static — and casts
`(LogLevel)level` assuming WTelegramClient's levels match `Microsoft.Extensions.Logging.LogLevel`
numerically. Map explicitly.

### 11.5 — S3 — `GetTelegramHash` is marked `//TODO: verify` and is wrong

Telegram's contacts hash algorithm operates on `int64` with a specific rotation; the current
implementation `hash ^ (id >> 21) ^ (id << 35) ...` shifts a `long` by 35 (well-defined but not the
spec) and will cause the server to always return a full contact list. Either implement the
documented algorithm or pass `0`.

---

## 12. GKit.DbMigration

Used by `BFer` for the legacy-database import.

### 12.1 — S2 — Every mapping registers a `p => false` default

```csharp
private Expression<Func<D, bool>> _defaultSelector = p => false;
...
if (_defaultSelector != null)                       // always true
    ctx.defaultMappings.Add(GetKeyMappingKey<S, D>(), _defaultSelector);
```

`Default(...)` is optional, but the field is pre-initialised, so the guard never fails. Every
mapping gets a default entry. Consequences: `DefaultAsync<D>()` finds two mappings ending in the
same destination type and throws "Multiple mapping found", or resolves the `p => false` selector
and `FirstAsync` throws `InvalidOperationException` on an empty sequence.

**Fix:** `private Expression<Func<D, bool>>? _defaultSelector = null;` and register only when set.

### 12.2 — S2 — Paging without an `OrderBy` can skip or duplicate rows

```csharp
var olds = await fromContext.Set<Old>().Where(filter).AsNoTracking().Skip(read).Take(BatchSize).ToListAsync();
```

`Skip`/`Take` without a total order is non-deterministic in SQL Server (and EF Core warns about
it). Across a 100k-row migration, rows are silently skipped and others migrated twice.

**Fix:** require a key selector expression and `OrderBy` it — or, better, use keyset pagination on
the source key, which also removes the O(n²) `Skip` cost.

### 12.3 — S2 — `TryGetSourceKey` compares boxed keys by reference

```csharp
var result = item.Where(kv => kv.Value == destinationKey).ToList();
```

`item` is `Dictionary<object, object>`; `==` on `object` is reference equality. For `int`/`long`/
`Guid` keys (i.e. all of them) this is **always false**. Both overloads of `TryGetSourceKey` are
therefore dead. **Fix:** `kv.Value.Equals(destinationKey)`.

### 12.4 — S2 — `LoadAsync` crashes on a partially-written log directory

```csharp
if (!Directory.Exists(MigrationLogsPath)) return Task.CompletedTask;
Dictionary<...> index = File.ReadAllLines(Path.Combine(MigrationLogsPath, "index.txt"))...
```

If the directory exists but `index.txt` does not (first-run crash, or `SaveChangesAsync` wrote a
mapping file before the index), this throws `FileNotFoundException` and the migration cannot
resume. `index[key]` also throws `KeyNotFoundException` for any stray file.

**Fix:** guard on the file, skip unknown files, and write `index.txt` first.

### 12.5 — S2 — Lookups miss mappings that live only in the pending-changes dictionary

```csharp
if (!keyMappings.TryGetValue(mapping, out var item) && !keyMappingsChanges.TryGetValue(mapping, out item))
    return false;
```

Short-circuit: if `keyMappings` has the type pair, `keyMappingsChanges` is never consulted, so a
mapping added earlier in the same batch is invisible. `MigrateEntity` calls `SaveChangesAsync`
after each batch, which merges them — so this is latent, but it breaks the moment anyone increases
`BatchSize` semantics or calls `Add` outside the batch loop.

### 12.6 — S2 — Seed failures are swallowed

```csharp
try { await ctx.toContext.AddAsync(entity); await ctx.toContext.SaveChangesAsync(); }
catch { ctx.toContext.ChangeTracker.Clear(); }
```

A bare `catch` with no logging around seed inserts hides connection failures, constraint
violations and mapping errors alike. The migration then proceeds against a half-seeded target.
**Fix:** catch `DbUpdateException` specifically (the intent is "already exists"), log, and rethrow
everything else.

### 12.7 — S3 — `Dispose` disposes contexts it does not own

`DbMigrationContext.Dispose()` disposes `fromContext`, `toContext` **and** `mappingStore`, all
injected. If they came from DI, DI disposes them again. Take an `ownsContexts` flag or don't
dispose.

### 12.8 — S3 — Progress reporting is inconsistent

`OnMigrationStarted` reports `CountSourceAsync()` (unfiltered count), while `MigrateEntity`
iterates `Where(filter).CountAsync()`. Progress percentages will be wrong whenever a filter is set.

### 12.9 — S3 — `DefaultAsync<D>()` throws the wrong exception when nothing is registered

`keys.First()` on an empty sequence throws `InvalidOperationException` before the intended
`ArgumentException("Default mapping not found")` is ever reached. Check `Any()` first.

---

## 13. GKit.Reporting

### 13.0 — S1 — `XlsReporter` throws `FileNotFoundException` at runtime: SkiaSharp is not deployed

Reproduced by every `XlsReporterTests` case before adding an explicit `SkiaSharp` reference:

```
System.IO.FileNotFoundException : Could not load file or assembly
'SkiaSharp, Version=3.119.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756'.
   at NPOI.SS.Util.SheetUtil.GetDefaultCharWidth(IWorkbook wb)
   at NPOI.XSSF.UserModel.XSSFSheet.AutoSizeColumn(Int32 column)
   at GKit.Reporting.XlsReporter`1.WriteReportAsync(...)  XlsReporter.cs:76
```

NPOI 2.8.0 declares `SkiaSharp 3.119.2`, but that package ships `ref/net8.0/SkiaSharp.dll` — a
*reference* assembly. On `net10.0` NuGet resolves only the compile-time asset, so nothing is
copied to the output folder and the runtime load fails the first time `AutoSizeColumn` runs.
`AutoSizeColumn` is called unconditionally at the end of `WriteReportAsync`, i.e. on the happy
path of the library's main entry point.

Reach: `XlsReporter` → `MudDataGridExtensions.ToXlsAsync` → every `Exportable="true"` grid in
BFer and StuffHR, plus `BFer/.../WasteMovementXlsReporter`.

**Fix (either, preferably both):**

1. `GKit.Reporting` declares the runtime dependency itself:
   ```xml
   <PackageReference Include="SkiaSharp" Version="3.119.2" />
   <PackageReference Include="SkiaSharp.NativeAssets.Linux" Version="3.119.2" />
   ```
   The `NativeAssets` package is required on Linux containers even once the managed assembly
   resolves.
2. Make auto-sizing opt-in (`XlsReporterOptions.AutoSizeColumns`, default `false`). It is also
   O(rows × columns) — see 13.6 — so most exports are better off without it.

Add a smoke test that renders a workbook end-to-end in CI; a compile-only test will not catch this.

### 13.1 — S2 — CSV output is not RFC 4180 escaped

```csharp
return string.Join(",", descriptors.Select(p => $"\"{p.SelectValue(row)}\""));
```

A value containing `"` produces broken CSV; a value containing a newline breaks the row. Italian
address/description fields regularly contain quotes.
**Fix:** `value.Replace("\"", "\"\"")`, and expose the separator (`;` is the Italian Excel default)
and encoding (Excel needs a UTF-8 BOM) as options.

### 13.2 — S2 — `CsvReporter.WriteReportAsync` disposes nothing and does not flush the encoder

`var writer = new StreamWriter(output);` is never disposed. `FlushAsync()` flushes the writer but
the `StreamWriter` is left holding the stream; combined with `WriteToStringAsync`'s
`using var ms` the last buffer can be lost for large outputs. Use
`await using var writer = new StreamWriter(output, leaveOpen: true)`.

### 13.3 — S2 — All cell values are written as strings

`XlsReporter` does `.SetCellValue(col.SelectValue(datum)?.ToString() ?? "")`. Numbers and dates
land in Excel as text: no sums, no sorting, and `1,5` vs `1.5` depends on the server's locale
(see 0.1). `ColumnDescriptor<T>` already exposes `Type` — use it:

```csharp
switch (value) {
  case null: cell.SetCellValue(""); break;
  case DateTime d: cell.SetCellValue(d); cell.CellStyle = dateStyle; break;
  case IConvertible c when descriptor.Type.IsNumeric(): cell.SetCellValue(c.ToDouble(CultureInfo.InvariantCulture)); break;
  default: cell.SetCellValue(value.ToString()); break;
}
```

### 13.4 — S2 — `XlsGroupingReporter` has quadratic-or-worse enumeration and a placeholder cell

`GroupingItem.AllSubNodes` and `.Depth` recompute the entire subtree on every access, and
`CreateGroupSection` calls `node.Items.Count()` and `node.AllSubNodes.Count()` 4–6 times each per
node, on `IEnumerable<T>` that is re-enumerated every time. The existing
`//TODO: use Parent to avoid AllSubNodes and Depth operations` says as much. On BFer's
`WasteMovementXlsReporter` over a year of movements this is the dominant cost.

There is also a literal placeholder: `.SetCellValue("-" ?? "")` at `XlsGroupingReporter.cs:380`
(`"-" ?? ""` is always `"-"`).

**Fix:** materialise `Items`/`Children` to arrays in `ExecuteGrouping`, cache `Depth` and a
subtree count, and set `Parent` as the TODO suggests.

### 13.5 — S3 — `XlsReporter` is not reusable

`_stylesCache` is keyed by method name but holds `ICellStyle` instances bound to the `IWorkbook`
created *inside* `WriteReportAsync`. Calling `WriteReportAsync` twice on the same reporter applies
styles from workbook #1 to workbook #2 — NPOI throws or silently misrenders.
**Fix:** clear the cache at the top of `WriteReportAsync`, or key it by workbook.

### 13.6 — S3 — `AutoSizeColumn` on every column is slow and needs fonts

`AutoSizeColumn` measures text and, on Linux, requires a font resolver; it is O(rows × columns).
For exports over a few thousand rows, make it opt-in.

### 13.7 — S3 — `IReporter<T>` declares `public abstract` members on an interface — redundant modifiers.

---

## 14. GKit.Pdf

### 14.1 — S2 — `"{0:N}"` formats every number with 2 decimals and group separators

```csharp
var isNumber = propertyValue is int or long or double or float or decimal;
var text = string.Format(formatProvider, isNumber ? "{0:N}" : "{0}", propertyValue);
```

An `int` quantity `1000` is stamped as `1.000,00` under `it-IT`. There is no way to override the
format per field. **Fix:** add `string? Format { get; init; }` to `PdfStamperFieldAttribute` /
`PdfStamperField<T>` and default to `"{0}"`.

### 14.2 — S2 — `PageNumber` is unvalidated

`pdfDocument.Pages[spec.PageNumber - 1]` throws `ArgumentOutOfRangeException` for `PageNumber = 0`
or beyond the document. Validate and throw a message naming the field.

### 14.3 — S3 — `XGraphics` objects are never disposed

`pages.Add(spec.PageNumber, new XTextFormatter(XGraphics.FromPdfPage(...)))` — the `XGraphics`
instances are held in a dictionary and dropped. Dispose them after saving.

### 14.4 — S3 — `AddGKitPdfServices` mutates global state and ignores its options

It sets the static `GlobalFontSettings` and returns; `GKitPdfOptions` is empty and `configure` is
invoked for nothing. Use `OperatingSystem.IsWindows()` instead of the legacy
`Environment.OSVersion.Platform` checks, and either use or remove the options type.

### 14.5 — S3 — `PdfReader.Open(stream)` leaves the caller's stream position undefined, and the document is not disposed.

---

## 15. GKit.SmartCardHost / GKit.SmartCardHost.Blazor

### 15.1 — S1 — `SmartCardHostServiceHandle` is never registered

```csharp
public static IServiceCollection AddGKitSmartCardHostService(this IServiceCollection ext)
{
    ext.AddScoped<SmartCardHostService>();      // ctor needs SmartCardHostServiceHandle
    return ext;
}
```

Resolving `SmartCardHostService` throws `InvalidOperationException: Unable to resolve service for
type 'SmartCardHostServiceHandle'`. **Fix:** `ext.AddScoped<SmartCardHostServiceHandle>();`

### 15.2 — S1 — `AddSmartCardHostCheck` was missing from `dev` — ✅ RESOLVED

`Stuff/StuffHR/StuffHR/Program.cs:95` calls it against 0.0.9, and it was absent from `dev`.

Root cause, from `git log`: it was never deleted on `dev`. Commit `f0e1f58` on **main** reverted
`GKit.SmartCardHost` to its pre-refactor shape (`SmartCardManager` → `SmartCardHostedService`,
`SmartCardStateBroker` deleted, `SmartCardState` restored). Commit `678b4d4` then added
`SmartCardHostHealthCheck` **on top of that older shape**. `dev` carries the broker refactor and
so never received the health check. `dev-cli` (`dafee28`) and `dev-radzen` (`28039b1`) each
re-applied the broker design over main and wired the health check to `SmartCardStateBroker`.

**Fix applied:** `GKit.SmartCardHost/SmartCardHostHealthCheck.cs` restored on `dev` verbatim from
`dev-cli` (`git checkout dev-cli -- …`; verified byte-identical to both `dev-cli` and
`dev-radzen`). The rest of the package is already identical across the three branches, so no
other porting was needed. `SmartCardHostHealthCheck` takes `SmartCardStateBroker`, whose
`Readers` property `dev` already exposes.

Guarded by `GKit.Tests/SmartCardHost/HealthCheckApiTests.cs` — a compile-time reference, so
deleting the API again breaks the build.

**Not pulled across:** `dev-cli` additionally carries `<IsPackable>true</IsPackable>` and
dependency bumps (`Microsoft.AspNetCore.Components.Web` 10.0.9 → 10.0.11, Nerdbank.GitVersioning
3.10.85 → 3.10.94) in the two SmartCardHost csproj files. Those are unrelated main-line updates;
applying them to `dev` would leave SmartCardHost as the only project out of step with the
10.0.9 / 3.10.85 pinning used by every other package on this branch. Bump the whole branch
together instead.

The underlying exposure remains: nothing would have caught this. See 0.5 —
`EnablePackageValidation` plus `PackageValidationBaselineVersion` turns an API removal into a
build failure.

### 15.3 — S2 — `SmartCardManager.OnCard` is `async void`

```csharp
protected async void OnCard(ISCardReader card)
```

Called from the `CardInserted` event handler, which wraps it in `using var card = new SCardReader(context)`.
The handler returns as soon as `OnCard` hits its first `await`, so **`card` is disposed while
`OnCard` is still using it**. The subsequent `card.Disconnect(...)` operates on a disposed reader.
Any exception after the first `await` also crashes the process (`async void`).

**Fix:** make `CreateMonitor`'s handler synchronous over a queue, or make `OnCard` return `Task`
and await it inside a `try/catch` in the handler, keeping `card` alive for the duration.

### 15.4 — S2 — `GetUid` ignores the APDU status word

```csharp
var buffer = new byte[10];
var error = card.Transmit([0xff, 0xca, 0, 0, 0], ref buffer);
error.ThrowIfNotSuccess();
return buffer!;   // includes the trailing SW1 SW2, and unused bytes
```

The `//TODO: verify result code` is accurate: the returned array still contains the `90 00` status
word and whatever was left in the 10-byte buffer, so `Convert.ToHexString(card.GetUid())`
(`SmartCardManager.cs:1405`) produces a UID with trailing garbage. If the card returns
`63 00` the method still "succeeds". **Fix:** check `SW1SW2 == 0x9000` and return
`buffer[..^2]` resized to the actual response length.

### 15.5 — S3 — Reader polling every 5 s with `JsonSerializer.Serialize` for comparison

`SmartCardManager.ExecuteAsync` compares reader arrays by serialising both to JSON every 5
seconds. Use `readers.SequenceEqual(availableReaders, StringComparer.Ordinal)`.

### 15.6 — S3 — `context.Establish` result is ignored on re-establish, and the hub has no group scoping (every browser tab receives every card read).

---

## 16. GKit.Mqtt

### 16.1 — S2 — `MqttLogger` passes the wrong arguments to the log template

```csharp
logger.Log(level, "[{Source}] {Message}", parameters);
```

`source` and `message` are never passed; MQTTnet's own `parameters` array is bound to
`{Source}`/`{Message}` instead. Structured logs contain the wrong values, and a mismatched
argument count produces `<unknown>` placeholders.

**Fix:** `logger.Log(level, exception, "[{Source}] {Message}", source, parameters is { Length: > 0 } ? string.Format(message, parameters) : message);`

Also `IsEnabled => true` bypasses the logging-level filter; return `logger.IsEnabled(...)`.

### 16.2 — S2 — Topic dispatch is exact-match only

`_handlers.TryGetValue(args.ApplicationMessage.Topic, out var topicHandlers)` — a handler
registered for `sensors/+/temp` subscribes successfully but never fires, because the *received*
topic is `sensors/3/temp`. Every wildcard route silently dead-letters with
"No handler registered for topic".

**Fix:** match with `MqttTopicFilterComparer.Compare(topic, filter)`.

### 16.3 — S2 — `StartAsync` connects synchronously and fails host startup

`IHostedService.StartAsync` awaits `ConnectAsync`. If the broker is unreachable the host does not
start, and there is no reconnect afterwards (the `//TODO: test and fix, due to the lack of a real
ManagedMqttClient` comment acknowledges this).

**Fix:** convert to `BackgroundService`, connect in `ExecuteAsync` with exponential backoff, and
handle `DisconnectedAsync` by reconnecting and resubscribing.

### 16.4 — S3 — `AddMqttClient` binds options at registration time

`config` is invoked immediately and the result registered as a singleton instance — no
`IConfiguration` binding, no `IOptions` validation, no reload. Use
`services.AddOptions<MqttServiceClientOptions<C>>().BindConfiguration(...)`.

### 16.5 — S3 — `MqttPayloadWrapper` rewrites payloads unconditionally and assumes UTF-8 text

`ConvertPayloadToString()` on binary payloads produces mojibake, and there is no opt-out per
topic. Also `PayloadSegment` is superseded by `Payload` in MQTTnet 5.

### 16.6 — S3 — `MqttServerEventsHandler` is constructed and dropped

`_ = new MqttServerEventsHandler(server, ext.Services);` in `MapMqtt` — nothing holds the
reference, so it is eligible for collection (its event subscriptions keep it alive via the server,
but `Dispose` is never called). Register it as a singleton.

### 16.7 — S3 — `MqttControllerBase` is an empty marker class; make it an interface or add the context (client id, topic, server) the handlers need.

---

## 17. GKit.Authentication.ActiveDirectory

### 17.1 — S2 — `Encoding.Default` for `memberOf`

```csharp
GetAttribute(resultsEntry.Attributes, "memberOf")?.GetValues(typeof(byte[]))
  .Select(p => Encoding.Default.GetString((byte[])p).ToLower())
```

LDAP returns UTF-8; `Encoding.Default` is UTF-8 on .NET Core but the intent is unclear and
`ToLower()` is culture-sensitive — under `tr-TR` `"I"` lowercases to `"ı"` and group matching
breaks. Use `Encoding.UTF8` and `ToLowerInvariant()`.

### 17.2 — S2 — The LDAP filter interpolates the username unescaped

```csharp
var query = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={username})...)";
```

LDAP filter injection: a username of `*)(objectClass=*` changes the filter's meaning. It is
constrained by the fact that the bind already succeeded with that username, so exploitability is
low — but escape it anyway per RFC 4515 (`*`, `(`, `)`, `\`, NUL).

### 17.3 — S2 — `AddActiveDirectoryAuthentication` uses `PostConfigure` for required options

`ext.PostConfigure(adConfig)` runs *after* configuration binding and after any other
`Configure` call — so a caller passing `adConfig` silently overrides `appsettings.json`, which is
backwards. There is also no `BindConfiguration("GKit:ActiveDirectory")` and no validation that
`Host`/`QueryBase` are non-empty. Use `AddOptions<ADAccountManagerOptions>().BindConfiguration(...).Configure(adConfig).ValidateOnStart()`.

### 17.4 — S3 — Non-Windows uses `AuthType.Basic` without requiring TLS

`IsSecure` defaults to `false` and `Port` to `389`, so the default Linux configuration sends the
password in cleartext. Default `IsSecure = true` / port 636, or fail fast when
`AuthType.Basic && !IsSecure`.

### 17.5 — S3 — `SignInAsync` returns `false` on `SignInAsync` failure but also when `HttpContext` is null — indistinguishable from bad credentials. `SignOutAsync` throws a bare `Exception`.

### 17.6 — S3 — The project uses `Microsoft.NET.Sdk.Web` with `<OutputType>Library</OutputType>`; it should use `Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App`.

---

## 18. GKit.Authentication.Blazor

### 18.1 — S2 — Open-redirect via `ReturnUrl`

```csharp
[SupplyParameterFromQuery] public string ReturnUrl { get; set; } = "/";
...
NavigationManager.NavigateTo(ReturnUrl);
```

`ReturnUrl` comes straight from the query string and is passed unvalidated to `NavigateTo`, in
both `LoginComponentBase` and `LogoutComponentBase`. `?returnUrl=https://evil.example` redirects
the freshly authenticated user off-site.

**Fix:**

```csharp
var target = Uri.TryCreate(ReturnUrl, UriKind.Relative, out _) ? ReturnUrl : "/";
NavigationManager.NavigateTo(target);
```

(ASP.NET Core's `Url.IsLocalUrl` is the MVC equivalent; replicate its checks — reject `//`,
`/\`, and anything with a scheme.)

### 18.2 — S3 — `AuthenticationOptions` is a `[CascadingParameter]` with no registration helper

`RedirectToLogin` / `RedirectToAccessDenied` default to `new()` when no cascading value is
supplied, so a consumer who configures `LoginPath` in `AddActiveDirectoryAuthentication` gets the
hard-coded `/account/login` in the Blazor components. Add
`services.AddCascadingValue(_ => options)` in an `AddGKitAuthenticationBlazor(...)` extension, and
share the path configuration with `GKit.Authentication.ActiveDirectory`.

---

## 19. Prioritised remediation plan

**Wave 1 — security and data integrity (do first)**

1. `GKit.SmtpHost` — remove the always-true default identity store (10.1)
2. `GKit.Authentication.Blazor` — validate `ReturnUrl` (18.1)
3. `GKit.PLC` — fix the CLR→S7 `VarType` mapping (9.1)
4. `GKit.RENTRI` — `IHttpClientFactory` (7.1), demo/live for anonymous clients (7.2), don't kill
   the host on 401 (7.3), thread-safe cache (7.4)
5. `GKit.EntityFramework` — per-context interceptor state (2.1)
6. Invariant culture across all persistence paths (0.1)

**Wave 2 — behaviour consumers are hitting today**

7. `GKit.MudBlazorExt` — `IgnoreQueryFilters` opt-in (4.1) and `AsNoTracking` assignment (4.2)
8. `GKit.OpcUa` — `EnsureConnected` in the factory (8.1) and direct-connection reconnect (8.2)
9. `GKit.PLC` — connection pooling / health-check churn (9.2)
10. `GKit.Quartz` — probe registration (1.1), `TimeSpanSchedule` semantics (1.2),
    `EnabledJobs`/`DisabledJobs` precedence (1.3)
11. `GKit.Application` — non-zero exit code (6.1)
12. `GKit.SmartCardHost.Blazor` — register the handle (15.1); `GKit.SmartCardHost` — `async void`
    over a disposed reader (15.3)
13. `GKit.Settings` — key prefix (3.1), `Setting` key mapping + DI helpers (3.2)

**Wave 3 — correctness of the reporting/migration paths**

14. `GKit.Reporting` — CSV escaping (13.1), typed cells (13.3), grouping performance (13.4)
15. `GKit.DbMigration` — default selector (12.1), ordered paging (12.2), `TryGetSourceKey` (12.3)
16. `GKit.Pdf` — number formatting (14.1)

**Wave 4 — packaging and hygiene**

17. Consolidate `Directory.Build.props`; enable package validation + baselines everywhere (0.5)
18. Drop stray dependencies (0.6); regenerate RENTRI stubs on `System.Text.Json`
19. Decide the fate of `GKit.TelegramHost` (11.1)
20. Add the test project (see `GKIT-TEST-PLAN.md`)
