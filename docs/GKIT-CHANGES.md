# GKit.* — applied changes (waves 1–4)

All four waves of `GKIT-REVIEW.md` are applied, plus the follow-ups listed at the end.

**187 tests, all passing** (159 unit + 28 bUnit component). 20/20 packages build and pack.
Zero compiler warnings.

```
dotnet test GKit.Tests/GKit.Tests.csproj
dotnet test GKit.Tests.Blazor/GKit.Tests.Blazor.csproj
```

---

## ⚠️ Read before upgrading

Four changes alter behaviour in ways that need action in the consuming projects.

### 1. `GKit.Settings` — stored values are now written and read invariantly

Existing rows written on an it-IT host hold values like `"1,5"` for a `decimal`, or
`"14/03/2026"` for a `DateTime`. The new reader parses invariantly and will throw
`FormatException` on them.

**Action:** before deploying, rewrite non-invariant values in the settings table:

```sql
SELECT [Key], [Value], [TypeName] FROM Settings
WHERE [Value] LIKE '%,%' OR [Value] LIKE '%/%';
```

Integers, strings, booleans and `TimeSpan`s are unaffected — `TimeSpan` already round-tripped
invariantly (verified by `TimeSpan_values_round_trip_regardless_of_the_ambient_culture`).

### 2. `GKit.EntityFramework` — audit timestamps moved to UTC

`ISoftDeletableEntity.DeletedAt` and `RevisionInfo.CreatedAt` now use `DateTime.UtcNow`. Rows
written by earlier versions hold local time, so a table will contain both until backfilled — a
one- or two-hour offset for Italy.

**Action:** backfill historical rows (minding CET/CEST) or record the cut-over date. Nothing
crashes either way; comparisons across the boundary are what drift.

### 3. `GKit.SmtpHost` — the server no longer accepts every credential

`ConfigurationSmtpIdentityStore` used to return `true` for any username and password, and was
the registered default. Any deployment relying on that is effectively unauthenticated.

```csharp
services.AddSmtpHost()
        .WithIdentityStore<ConfigurationSmtpIdentityStore>()   // or your own
        .AddControllersWithRoutes();
```

```json
{ "SmtpHost": { "Users": { "erp": "<password>" } } }
```

Without this the new `DenyAllSmtpIdentityStore` rejects every login and logs the fix.

### 4. `GKit.TelegramHost` — now opt-in

`ExecuteAsync` was commented out wholesale, so the package registered a hosted service that
silently dropped every message. It is restored behind `TelegramHost:Enabled` (default `false`),
with retry and no host-killing exceptions. Set `Enabled: true` to connect.

---

## Breaking API changes

| Package | Before | After | Why |
|---|---|---|---|
| EntityFramework | `RegisterForHardDelete(entity)` | `RegisterForHardDelete(context, entity)` | registrations must be scoped to a context |
| EntityFramework | `RegisterForUpdate(entity)` | `RegisterForUpdate(context, entity)` | as above |
| EntityFramework | `RegisterForRevisionReferenceUpdate(entity)` | `…(context, entity)` | as above |
| EntityFramework | `DisableSoftDelete(context, entity, hard)` | `DisableSoftDelete(context, entity)` | the `hard` parameter was never read |
| EntityFramework | `protected AdjustNewRevisionReferences(...)` | removed | replaced by context-scoped handling |
| Reporting | `CsvReporter<T>(descriptors)` | `CsvReporter<T>(descriptors, CsvReporterOptions?)` | separator / BOM / line terminator |
| Reporting | `GroupingItem.Items` is `IEnumerable<T>` | `IReadOnlyList<T>` | materialised once instead of re-enumerated |
| Quartz | `[TimeSpanSchedule]` | `[DailyAtSchedule]` / `[IntervalSchedule]` | the old name did not mean what it said (see below) |
| Quartz | `GetTriggers()` → `IEnumerable<object>` | `IEnumerable<ITrigger>` | it always returned triggers |
| PLC | `IPlcContextOptions.Port` is `short` | `int` | TCP ports exceed `short.MaxValue` |
| RENTRI | `BaseClientFactory<T>(ApiStatusProvider)` | `…(ApiStatusProvider, IOptions<RentriOptions>)` | anonymous clients need an environment |
| RENTRI | `BuildClient(ClientOptions)` | `BuildClient(ClientOptions?, string?)` + `abstract RentriApi Api` | typed status routing, anonymous base URL |
| RENTRI | `RentriHttpClientFactory.Create()` | `IServiceCollection.AddRentriHttpClient()` | pooled handlers |
| RENTRI | `XxxClient(HttpClient, ClientOptions)` | `XxxClient(HttpClient, ClientOptions?, string?)` | as above |
| Application | `Wrap` returns `void` | plus `RunAsync`/`WrapAsync` returning an exit code | orchestrators read the exit code |

`[TimeSpanSchedule]` still compiles and keeps its **existing behaviour** (daily-at), marked
`[Obsolete]` pointing at the two unambiguous replacements. Nothing silently changes schedule.

None of the removed/changed signatures are called directly by AllGlass, BFer or StuffHR —
verified by grep. They all go through the extension methods and `CreateClient(options)`.

---

## Wave 1 — security and data integrity

**GKit.EntityFramework**
- **2.1 (S1)** Interceptor registrations moved from shared `List<object>` fields to a
  `ConditionalWeakTable<DbContext, …>`. One context's `SaveChanges` can no longer clear or
  consume another's hard-delete / no-revision registration.
- **2.2 (S3)** `SavingChangesAsync` genuinely async — awaits `entry.ReloadAsync` instead of
  blocking a thread pool thread per modified entity.
- **2.3 (S2)** `CopyFromObject` forwards `excludeProps`, which it dropped.
- **2.3a (S2)** `exclude:` now works for value types. `p => p.Id` on an `int` boxes to
  `Convert(p.Id, Object)`, matching neither branch — so *every* exclusion of an
  `int`/`decimal`/`DateTime`/`bool`/`Guid` property was ignored, including the primary-key
  exclusions this API exists for.
- **0.3 (S2)** Reflection copying skips get-only properties and indexers.
- **2.5/2.7 (S3)** UTC timestamps; `ReferenceEquals` and indexer-safe reference fix-up.

**GKit.Settings**
- **0.1 (S1) / 0.2 (S2)** `SettingValueConverter`: invariant round-trip plus `Nullable<T>`,
  enums, `Guid`, `Uri` — none of which `Convert.ChangeType` handles.
- **3.5 (S3)** `UpdateOptionsAsync` awaits *every* `OptionsChanged` handler.

**GKit.Reporting**
- **13.0 (S1)** Declares `SkiaSharp` + `SkiaSharp.NativeAssets.Linux`. NPOI's `AutoSizeColumn`
  needs SkiaSharp at runtime; NPOI declares it but only its *reference* assembly resolves on
  net10.0, so `WriteReportAsync` threw `FileNotFoundException` on the happy path. The test
  project deliberately does **not** reference SkiaSharp — the XLS tests prove it flows.
- **13.3 (S2)** Typed cells: numbers, dates and booleans use their native cell type, so they
  are summable and sortable and render in the *reader's* locale.
- **13.5 (S3)** `ResetStyles()` per render — a reused reporter threw
  "This Style does not belong to the supplied Workbook".

**GKit.PLC — 9.1 (S1)**
- `FromClrType` rewritten: `bool→Bit`, `short→Int`, `ushort→Word`, `int→DInt`, `uint→DWord`,
  enums via their underlying type. A `DInt` of `-1` no longer reads back as `4294967295`.
- Unmappable types return `null` instead of defaulting to `DWord`; `ModelBuilder.Validate()`
  fails the build naming the property.
- `ToPlcAddress` no longer overwrites the width the address itself encodes.

**GKit.SmtpHost 10.1 (S1)**, **GKit.Authentication.Blazor 18.1 (S1)**, **GKit.RENTRI 7.1–7.4,
7.9 (S1)** — as described in the sections above and in `GKIT-REVIEW.md`.

---

## Wave 2 — behaviour consumers hit today

**GKit.MudBlazorExt**
- **4.1 (S1)** `IgnoreQueryFilters` is now an opt-in `[Parameter]`, default off. It was applied
  unconditionally, defeating `WithSoftDelete()` so every grid showed deleted rows and superseded
  revisions — the reason BFer hand-rolls `WithoutDeleted()`/`WithoutNotActive()` on ~12 query
  sites and has an `IgnoreDependantQueryFilters()` helper. Export uses the same query shape, so
  the XLSX and the grid finally agree.
- **4.2 (S2)** `query = query.AsNoTracking()` — the result was discarded, so virtualised
  scrolling grew the change tracker without bound.
- **4.3 (S2)** `OnLoadedServerData` fires once, not twice.
- **4.7/4.8/4.9** Removed the identical-retry `AttachRange`; the semaphore is taken before the
  attach and released only if taken (no more `SemaphoreFullException`), and is disposed;
  exceptions are logged with their stack trace. Download renamed `.xls` → `.xlsx`.

**GKit.OpcUa**
- **8.1 (S1)** `CreateContextAsync` calls `EnsureConnected`, not `RenewConnection`. It was
  closing and disposing the pooled session on every call — so AllGlass's 20-second job and every
  health-check probe destroyed and rebuilt the shared session, dropping subscriptions.
- **8.2 (S1)** Direct (non-reverse) connections now reconnect; the recovery path was gated on
  `ReverseConnectManager is not null`, which AllGlass does not use.
- **8.3 (S2)** `GetOrAdd` instead of `TryAdd` — the losing instance of a race was leaked
  undisposed. Health check reports `Unhealthy` instead of throwing.

**GKit.PLC**
- **9.2/9.3 (S1/S2)** `EnsureConnected` is guarded by a semaphore and closes the previous socket
  before opening a new one; two concurrent reads used to leak one socket per race, and S7-300/400
  CPUs allow only a handful of concurrent PG/OP connections.
- **9.4/9.5** `Port` widened to `int`; `Dispose` no longer blocks on `DisposeAsync`.

**GKit.Quartz**
- **1.1 (S1)** `QuartzProbe` is registered by `AddGKitQuartz`. The probe job is scheduled
  unconditionally, so a host that skipped `AddQuartzCheck` hit an unresolvable dependency on
  every heartbeat, forever.
- **1.2 (S1)** `[IntervalSchedule]` really repeats; `[DailyAtSchedule]` says daily-at explicitly.
- **1.3 (S2)** Allow/deny precedence fixed — and **deny now wins**, so a `DisabledJobs` entry is
  an effective kill switch even alongside an allow-list. Writing the test is what forced that
  decision; the first implementation was allow-only.
- **1.4/1.5/1.6/1.7** Discovery skips interfaces, abstract types and open generics and survives
  `ReflectionTypeLoadException`; job keys are deterministic and trigger keys unique;
  `IsNullOrWhiteSpace` validation with `ValidateOnStart`; `GetTriggers` returns `ITrigger`.
- Logic extracted to a testable `JobScheduleResolver` (15 tests).

**GKit.Application** — **6.1 (S1)** non-zero exit code on fatal error; `using var host`;
`--help` recognised anywhere in the arguments; the Development migration skip is logged.

**GKit.SmartCardHost** — **15.1 (S1)** `SmartCardHostServiceHandle` registered;
**15.2 (S1)** `AddSmartCardHostCheck` restored (see `GKIT-REVIEW.md` §15.2);
**15.3 (S2)** `OnCard` is no longer `async void` operating on a disposed reader;
**15.4 (S2)** `GetUid` validates the APDU status word and strips it.

**GKit.Settings** — **3.1** key prefix includes its separator; **3.2** `AddGKitSettings()`
model helper plus `AddDbSettings`/`AddDefaultSettings` DI extensions.

---

## Wave 3 — reporting and migration correctness

**GKit.Reporting**
- **13.1/13.2 (S2)** RFC 4180 quoting (embedded quotes doubled), configurable separator
  (`;` for Italian Excel), line terminator and UTF-8 BOM; the writer is disposed so the encoder
  is flushed.
- **13.4 (S3)** Grouping materialises once and caches `Depth`/`AllSubNodes` (they were recursive
  and recomputed on every access, with `Items.Count()` called 4–6 times per node); `Parent` is
  now set as the old TODO suggested; null group keys no longer NRE.

**GKit.DbMigration**
- **12.1 (S2)** The default selector was pre-initialised to `p => false`, so the `!= null` guard
  never failed and every mapping registered a default — making `DefaultAsync<D>()` either report
  "multiple mappings" or throw on an empty sequence.
- **12.2 (S2)** Paging is ordered by the source primary key. `Skip`/`Take` without a total order
  silently skips and duplicates rows across a large migration.
- **12.3 (S2)** `TryGetSourceKey` compared boxed keys with `==` (reference equality), so for
  `int`/`long`/`Guid` keys it was always false — both overloads were dead.
- **12.4 (S2)** `LoadAsync` survives a directory with no `index.txt` (the state a crash leaves)
  and skips orphan log files; the index is written *before* the data files.
- **12.5 (S2)** Lookups consult pending changes as well as loaded ones.
- **12.6/12.7/12.8/12.9** Seed failures catch `DbUpdateException` only and are logged; injected
  contexts are not disposed unless `OwnsDependencies`; progress honours the filter;
  `DefaultAsync` checks emptiness before ambiguity. Duplicate `Add` names the entity and key.

**GKit.Pdf** — **14.1** per-field `Format` (`"{0:N}"` was forced on every number, so an
integer `1000` stamped as `1.000,00` under it-IT); **14.2** page range validated;
**14.3** `XGraphics` and the document are disposed.

---

## Wave 4 — packaging and hygiene

- **0.5** `Directory.Build.props` now carries shared package metadata, `GenerateDocumentationFile`
  and `EnablePackageValidation` for every `GKit.*` package; the 23 per-project
  Nerdbank.GitVersioning overrides are gone (one pinned version).
- **`GKit.SmartCardHost` was not packable at all.** `Microsoft.NET.Sdk.Web` defaults
  `IsPackable` to false, so the package silently never built from this branch even though
  StuffHR consumes it from NuGet. Found by running `dotnet pack` across the suite; now fixed and
  verified — `AddSmartCardHostCheck` is present in the packed assembly.
- **0.6** Removed the four stray `using NPOI.SS.Formula.Functions;`, the unused
  `System.Security.Cryptography.Xml` references, and `Microsoft.EntityFrameworkCore.SqlServer`
  from `GKit.DbMigration` — which was dragging `Microsoft.Data.SqlClient`, `Azure.Identity` and
  `Azure.Core` into every consumer.
- **11.1** `GKit.TelegramHost` restored behind an `Enabled` flag rather than shipping a no-op.
- New `.github/workflows/ci.yaml`: tests on Linux **and Windows** (`DateTime`, PEM certificates
  and SkiaSharp native assets all differ), with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` so
  the culture tests are not vacuous, plus a `pack` job.

`PackageValidationBaselineVersion` is deliberately **not** set in `Directory.Build.props` —
baseline validation must restore the published package, which would break offline builds. Set it
in CI (`dotnet pack -p:PackageValidationBaselineVersion=0.0.24`) to make an API removal fail the
build, which is what would have caught `AddSmartCardHostCheck` disappearing.

---

## Action required in AllGlass

The PLC fixes correct two live bugs but expose two wrong address strings. All properties on
`TermofarCurrentProductionStatus` are `uint`, which fell through to the old `DWord` default and
overwrote the address-derived width. Compared against the node-red `vartable` in the same file:

| Property | Address in model | vartable | Old read | New read | Verdict |
|---|---|---|---|---|---|
| `TargetPacks` | `DB200.DBD210` | `DINT210` | DWord | DWord | unchanged |
| `CurrentPacks` | `DB200.DBD216` | `DINT216` | DWord | DWord | unchanged |
| `TotalPacks` | `DB200.DBD220` | `DINT220` | DWord | DWord | unchanged |
| `MinPacks` | `DB200.DBW136` | `WORD136` | **DWord — 4 bytes at a 2-byte field** | Word | **fixed** |
| `MachineState` | `DB200.DBW134` | `WORD134` | **DWord — spilled into `MinPacks`** | Word | **fixed** |
| `CurrentFlasks` | `DB200.DBB224` | `DINT224` | DWord | **Byte** | ⚠️ **fix in AllGlass** |
| `TotalFlasks` | `DB200.DBB228` | `DINT228` | DWord | **Byte** | ⚠️ **fix in AllGlass** |

`CurrentFlasks`/`TotalFlasks` have wrong *address strings* (`DBB` = byte) that the old `DWord`
default was accidentally compensating for:

```diff
-  .Property(p => p.CurrentFlasks).ToPlcAddress("DB200.DBB224");
+  .Property(p => p.CurrentFlasks).ToPlcAddress("DB200.DBD224");
-  .Property(p => p.TotalFlasks).ToPlcAddress("DB200.DBB228");
+  .Property(p => p.TotalFlasks).ToPlcAddress("DB200.DBD228");
```

Not applied — it is another repository and the PLC layout should be confirmed against the
machine first.

---

## Follow-ups (completed after the four waves)

### `RevisionInterceptor` no longer queries the database

`entry.Reload()` is replaced by `entry.CurrentValues.SetValues(entry.OriginalValues)`. The
original values are already tracked, so recovering them needs no round trip — removing a
synchronous `SELECT` per modified entity issued on the same connection while the save was in
flight. `SavingChangesAsync` is synchronous again because there is nothing left to await.

Proven, not assumed: `Creating_a_revision_issues_no_extra_query` counts the SQL EF actually
executes. My first attempt counted `ExecuteReader` calls and failed at 2 — on SQLite an
`INSERT ... RETURNING` also goes through a reader, so the save batch itself was being counted.
The `CommandCounter` test double now classifies by command text.
`The_superseded_row_keeps_its_original_values` covers the behaviour the revert exists for.

### RENTRI: the items I had wrongly deferred

`BaseClient` exists precisely so the NSwag stubs can be regenerated without losing the
extensions layered on them — so these needed no generated-code edits after all:

- **7.5** `Retry-After` uses `HttpResponseHeaders.RetryAfter`, handling both delta-seconds and
  HTTP-date. `TimeSpan.Parse("120")` meant **120 days**, so any backoff honouring it never retried.
- **7.6** `UseContextAsync` / `WithContext` no longer block a thread pool thread for the duration
  of a remote call. The blocking `UseContext()` remains for compatibility, documented as such.
- **7.7** The JWT carries explicit `iat`/`nbf`/`exp` (5 minutes) instead of
  `JsonWebTokenHandler`'s 60-minute default.
- **0.4** The integrity digest uses `SHA256.HashData(content.ReadAsStream())` — genuinely
  synchronous rather than a blocking wait on a `Task`. `PrepareRequest` is a synchronous partial
  on the stub, so this is the correct fix within that constraint.
- Paging headers parse invariantly and tolerate a missing or malformed value.

### Tier C — bUnit component tests

`GKit.Tests.Blazor` (28 tests). Implementing them required fixing the defects they cover:

- **5.1** `DocumentEventSourceBase.DisposeAsync` passes the `DotNetObjectReference` that
  `connect` registered — it passed `this`, which interop serialises as JSON, so the JS side
  could never match the listener. Every navigation leaked a document handler plus the reference.
- **5.2** `DbContextProvider` re-emits the cascading value its base provides (overriding the
  markup had discarded it) and holds children back until `Context` exists.
- **5.3** `TimerService` observes its async callbacks, surfaces failures via `OnCallbackError`,
  and uses the `TimeSpan` timer overloads (the old `(int)` casts overflowed past ~24.8 days).
- **4.4** `ToXlsAsync` skips columns whose `PropertyName` is not a plain property chain instead
  of NRE-ing the whole export.
- **4.5** `EditEntityDialog` can save a pre-filled model (Save was gated on `IsTouched`, so
  accepting `NewValueFactory` defaults left it permanently disabled); guarded against double
  submission instead.
- **4.6** The dialog's JS interop runs on first render only, not on every keystroke.

`ExportColumnTests` renders a real `MudDataGrid` (with `MudPopoverProvider`, as a real layout
would). The `EntityGrid` query tests deliberately exercise `BuildQuery` on the component
instance without a renderer: that is where the defects were, and it avoids a brittle dependency
on the grid's full rendering stack.

### `Newtonsoft.Json` stays

Deferred by decision, not oversight: the NSwag stubs do not serialise correctly under
`System.Text.Json`. Revisit when NSwag or the RENTRI API changes.

### Tier D retired; its concerns pulled into hermetic tests

The integrated services are not available to this project, so a tier D suite would be
permanently skipped — which reads as covered when it is not. Most of what it was meant to catch
turned out to be pure logic hiding behind an integration boundary, and three conversions
**required fixing the defect first**:

- **16.1 (S2)** `MqttLogger` bound MQTTnet's own parameter array to `{Source}`/`{Message}`, so
  the structured log carried the wrong values and source/message were never recorded at all.
  `IsEnabled` was hard-coded `true`, bypassing the configured level.
- **16.2 (S2)** New `MqttTopicRouter`: dispatch was an exact dictionary lookup on the topic, so
  a handler for `sensors/+/temp` subscribed successfully and then never fired for
  `sensors/3/temp`. Every wildcard route silently dead-lettered.
- **17.2 (S2)** New `LdapFilter.Escape`: the username was interpolated into the search filter
  unescaped.
- **17.1 (S3)** `Encoding.Default` → `Encoding.UTF8`, `ToLower()` → `ToLowerInvariant()` (under
  `tr-TR`, "I" lowercases to "ı" and group matching silently stops working).
- **17.3/17.4 (S3)** Options are bound then configured then validated (`PostConfigure` ran
  *after* binding, so a caller delegate silently overrode `appsettings.json`), and the defaults
  are LDAPS/636 — an unencrypted Basic bind now fails fast unless explicitly acknowledged.
- **10.2 (S2)** `SmtpRouteAttribute` matches the mailbox **address**, falling back to the
  display name. Matching `InternetAddress.Name` — usually empty — meant `erp@.*` never matched
  anything. A message parsed without a Subject header no longer throws, and patterns are
  compiled once with a match timeout.
- **10.3 (S2)** New `SmtpRouteTable`: controller discovery walked every assembly and every type
  **per message**. Now built once at startup.
- **10.4 (S2)** Dead letters are written as complete `.eml` — headers, HTML bodies and
  attachments — with collision-free names. They were JSON with only `TextBody`, discarding the
  very thing a dead letter exists to preserve.
- **8.3** `OpcUaConnectionPool` gained `GetOrCreate`/`Remove`, making the session bookkeeping
  (identity per options, single creation under a race, removal) directly testable.

`GKIT-TEST-PLAN.md` has the mapping table and, for the residue that genuinely needs the
physical thing, a 15-minute manual smoke checklist — starting with the AllGlass PLC address
corrections, since the wave 2 changes alter what those properties read.

## Still deliberately undone

- **Five checks need the real device or endpoint** and no double substitutes: the PLC returning
  the expected bytes at a configured address, an OPC UA session surviving a real network drop,
  a reader producing the badge's printed UID, RENTRI accepting the signed JWT, and an LDAP bind
  against the real directory. These are the manual checklist.
- `System.Security.Cryptography.Xml` still carries 5 known high-severity advisories at 10.0.9.
  The explicit reference is a **pin, not a stray dependency** — I removed it during the wave 4
  cleanup and NPOI's transitive 8.0.2 (8 advisories) took its place, so it is back with a
  comment explaining why. Bump when a patched version ships.
- `GKit.TelegramHost` is restored but unexercised: it needs a Telegram account to test at all,
  and no consumer uses it. It is off by default and says so.
