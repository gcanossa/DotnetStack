# Proposal — a solution scaffolder for the GKit suite

Status: proposal, rev 2 (branch `dev-cli`)
Scope: two new projects — `GKit.Templates` and `GKit.Cli`. No changes to existing packages.

**Decisions taken**

1. Two deliverables: a `dotnet new` template pack (`GKit.Templates`) and a `dotnet tool` front end (`GKit.Cli`, command `gkit`). The CLI shells out to `dotnet new`, so templates have exactly one source of truth.
2. AllGlass / BFer / FabbriFC / Millesrl / Stuff are **reference material only** — the same rule as the UI proposal. They are not migration targets here, but they are what the templates are inferred from.
3. Local accounts are a library, not template content: **`GKit.Authentication.Simple`** (added on this branch).
4. The template default culture is **it-IT**, implemented by referencing `GKit.UI.Localization` and calling `AddGKitUiLocalization()` — *not* by hard-coding cultures. This is compatible with decision 7 of the UI proposal, which makes neutral English the *library* default.
5. `--ui` is a first-class axis over `GKit.UI.MudBlazorExt` / `GKit.UI.RadzenExt`, never a boolean.

Rev 2 note: rev 1 predated the UI split and proposed a template-side `.ui/*` content fork covering the whole `Components/` tree, plus an `AddAppUiServices()` indirection to keep `Program.cs` identical across UIs. The UI abstraction makes both largely unnecessary — see §5.

---

## 1. What the reference projects actually show

Three repos are current GKit consumers; two more are the pre-GKit ancestry.

| Repo | Entry project | Stack | Deployment |
|---|---|---|---|
| AllGlass | `ErpPalletLink` | net10, MySQL, OPC UA + PLC + Quartz | win-x64, Windows service |
| BFer | `Benati.Rifiuti.WebApp` (+ `.Migrator`) | net10, SqlServer, RENTRI + Pdf + Reporting | win-x64, Windows service |
| Stuff | `StuffHR` | net10, Npgsql, SmartCard + Quartz + Settings | linux-arm64, single file |
| FabbriFC | `FC.TimeTracker` | net8, `GC.*` packages, AD auth | win-x64 |
| Millesrl | `Mille.Eventi`, `TLM`, … | net7 / net4.x | win-x64 |

`Millesrl/EventManager/{SmtpHost,TelegramBotEndpoint,TelegramHost}` are the in-repo originals of
`GKit.SmtpHost` / `GKit.TelegramBotEndpoint` / `GKit.TelegramHost`. The suite is what got extracted
from these apps; the templates are the other half of that extraction — what gets put back.

### The invariant skeleton, measured

Distinct wiring calls (`builder.Services.Add*`, `builder.WebHost.*`, `app.Use*`, `app.Map*`,
`Application.Wrap`, `ApplyPendingMigrations`) per entry project, and the intersection:

```
ErpPalletLink           22 distinct calls
Benati.Rifiuti.WebApp   28
StuffHR                 34
                        ──
common to all three      15
```

The 15:

```
Application.Wrap                        app.MapHealthChecksJson
builder.Services.AddSerilog             app.MapRazorComponents
builder.Services.AddRazorComponents     app.UseRequestLocalization
builder.Services.AddLocalization        app.UseStaticFiles
builder.Services.AddMudServices         app.UseAntiforgery
builder.Services.AddMudTranslations     builder.Services.AddHealthChecks
builder.Services.AddGKitBlazorServices   builder.Services.AddDbContextFactory
builder.Services.AddSingleton
```

So 44–68% of each app's distinct wiring is identical, **in the same order**, down to
`options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft` and
`CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("it-IT")`. The
`AddDbContextFactory` bodies share a distinctive idiom in all three:

```cs
var dbOptions = builder.Environment.EnvironmentName switch
{
    _ => options.UseX(connectionString, b => b.MigrationsAssembly(…)),
};
```

Below `Program.cs` the convergence continues: `AppSettings.cs` + `Configure<AppSettings>(…GetSection("App"))`,
`Components/{Layout,Pages,Dialogs,Forms,Grids,Validators,Shared}`, `Data/`, `Jobs/`, `Services/`,
`CliRunners/` with a `CommandLineRunnerBase`, `certs/` + a `cert_renew` script, and at solution root
`Directory.Build.props`, `version.json`, `build/`, `release.sh`, `resetdb`, `docker-compose.yml`.

`release.sh` is copy-pasted three times and differs only in RID and project list — and has already
drifted: AllGlass and BFer lack the `mkdir ./build` guard StuffHR has, so a fresh clone fails.

### The axes of variation

| Axis | Observed values |
|---|---|
| DB provider | MySQL · SqlServer · Npgsql · Sqlite/InMemory |
| Data layering | in-app `Data/` (StuffHR, AllGlass) vs `X.Data` + `X.Data.EF.<Provider>` with `Manifest.cs` + `Migrations/` (BFer, FC.TimeTracker) |
| Auth | none · cookie + local accounts · ActiveDirectory · Entra |
| Deployment | `win-x64` + `AddWindowsService()` vs `linux-arm64` + container |
| Capability packs | OPC UA, PLC, Quartz, RENTRI, Pdf, Reporting, SmartCard, Settings, MQTT, Smtp, Telegram |
| Extra projects | `.Migrator` console on `GKit.DbMigration` · test project |

### The friction the CLI exists to remove

1. **Reference toggling.** All three keep a commented-out block of `ProjectReference`s to
   `../../../Personal/DotnetStack/GKit.*` beside the `PackageReference`s, swapped by hand
   (`ErpPalletLink.csproj`, `Benati.Rifiuti.WebApp.csproj`, `StuffHR.csproj`).
   `Benati.Rifiuti.Data.csproj` is worse — a raw `<Reference>` with a `HintPath` into
   `GKit.RENTRI/bin/Debug/`, which only resolves on one machine.
2. **Version drift.** No solution uses Central Package Management:

   | Package | AllGlass | BFer | StuffHR |
   |---|---|---|---|
   | `GKit.Application` | 0.0.17 | 0.0.17 | 0.0.19 |
   | `GKit.BlazorExt` | 0.0.21 | 0.0.21 | 0.0.24 |
   | `GKit.MudBlazorExt` | 0.0.32 | 0.0.32 | 0.0.35 |
   | `GKit.Reporting` | — | 0.0.12 | 0.0.15 |

3. **Script drift**, as above.

## 2. Deliverables

| Project | Kind | Purpose |
|---|---|---|
| **`GKit.Templates`** | `PackageType=Template` | The templates and the feature manifest. No compiled output. |
| **`GKit.Cli`** | `PackAsTool`, `ToolCommandName=gkit` | Generation front end plus the lifecycle commands that `dotnet new` cannot express. |

The pack alone would scaffold, but §1's friction points are lifecycle, not scaffolding — which is
what justifies the second project rather than shipping templates on their own.

## 3. Template catalog

### Solution

`gkit-sln` emits `.slnx`, `Directory.Build.props`, **`Directory.Packages.props`** (new — CPM, closing
friction 2), `version.json`, a single parameterised `release.sh`, `resetdb`, `docker-compose.yml`
matched to `--db`, `.gitignore`, `README.md`, and `.github/workflows/ci.yaml` modelled on the one
this repo now has.

### Projects

| Template | Modelled on | Notes |
|---|---|---|
| `gkit-app` | StuffHR / ErpPalletLink / Benati.WebApp | Blazor-Server host: the §1 skeleton, `AppSettings.cs`, Components tree, `CliRunners/CommandLineRunnerBase.cs`, `appsettings.*.json`, `certs/` + `cert_renew` |
| `gkit-shared` | `Test.Repo.UI.Shared` | Plain (non-Razor) library: model, validators, query factories, DbContext. References neither UI adapter — the boundary is compiler-enforced |
| `gkit-data` | `Benati.Rifiuti.Data` (+`.EF.<Provider>`) | Only with `--data-layout split`; emits `Manifest.cs` + `Migrations/` |
| `gkit-migrator` | `Benati.Rifiuti.Migrator` | Console host on `GKit.DbMigration` |
| `gkit-worker` | Mille EventManager, TLM | Headless `Host`; Quartz / Mqtt / SmtpHost / TelegramHost, no Blazor |
| `gkit-test` | **`GKit.Tests`** | xunit + `SqliteFixture` + bUnit — *not* the older `Test.Repo` shape |

### Items

`gkit-crud --entity Vehicle` is the one that repays the effort: BFer and StuffHR both repeat a
Grid + Form + Dialog + Validator + Page quintuple per entity. It splits along the boundary the UI
work now enforces:

| Emitted into | Files |
|---|---|
| the `.Shared` project (UI-neutral) | `Model/Vehicle.cs`, `Validation/VehicleValidator.cs` (on `AbstractValidatorBase`), the `Func<DbContext, IQueryable<Vehicle>>` query factory |
| the host (per `--ui`) | `Components/Grids/VehicleGrid.razor`, `Forms/EditVehicleForm.razor`, `Dialogs/EditVehicleDialog.cs`, `Pages/Vehicles.razor`, and the `AddGKitValidator<Vehicle, VehicleValidator>()` line |

Also `gkit-job` (Quartz job + `[CronSchedule]`/`[TimeSpanSchedule]`), `gkit-clirunner`,
`gkit-opcua-context`, `gkit-plc-context`, `gkit-report`.

## 4. Options

```
--ui            mudblazor | radzen | none                       (default mudblazor)
--db            sqlserver | npgsql | mysql | sqlite | none      (default sqlserver)
--data-layout   inline | split
--auth          none | simple | activedirectory | entra
--host          windows-service | systemd | docker
--rid           win-x64 | linux-x64 | linux-arm64
--features      quartz,opcua,plc,mqtt,pdf,rentri,reporting,settings,smartcard,smtp,telegram,dbmigration
--culture       it-IT (default)  --cultures en-US,it-IT
--health --https-certs --cli-runners --tests                    (all on by default)
--gkit-version  latest | <version>
```

Every `--features` entry drives four coordinated edits — `PackageReference`, `using`, the `Add…`
call in `Program.cs`, and the `Add…Check` in the health-check chain. That coupling is currently
manual and is where new projects most often end up half-wired.

`--auth` maps onto the two hardened managers: `simple` → `GKit.Authentication.Simple`
(`AddSimpleAuthentication<TContext,TUser>`, `GKit:SimpleAuthentication` section), `activedirectory`
→ `GKit.Authentication.ActiveDirectory` (`GKit:ActiveDirectory`). Both are cookie-based with the same
claim set, so the generated login/logout pages on `LoginComponentBase<T>`/`LogoutComponentBase` are
identical apart from the injected manager.

## 5. How the UI axis is handled

The UI abstraction changed what this costs. Because the two adapters ship **identical component
names in distinct namespaces**, and an app references exactly one, switching is a package swap plus
one `@using` line plus one service call:

| | MudBlazor | Radzen |
|---|---|---|
| package | `GKit.UI.MudBlazorExt` + `MudBlazor` | `GKit.UI.RadzenExt` + `Radzen.Blazor` |
| `_Imports.razor` | `@using GKit.UI.MudBlazorExt` + `@using MudBlazor` | `@using GKit.UI.RadzenExt` + `@using Radzen{,.Blazor}` |
| `Program.cs` | `AddMudServices(); AddGKitMudBlazorUi();` | `AddRadzenComponents(); AddGKitRadzenUi();` |
| shared by both | `AddGKitBlazorServices()`, `AddGKitUiLocalization()`, and everything in `GKit.UI.Data` — each adapter call chains `AddGKitUiCore()` itself | |

So the template splits as:

```
gkit-app/
  .shared/       Program.cs, AppSettings.cs, Data/, CliRunners/, Jobs/, Services/, appsettings.*.json
  .ui/mudblazor/ _Imports.razor, App.razor, Components/Layout/, Components/Pages/, wwwroot/
  .ui/radzen/    (same shape)
```

`Program.cs` stays shared with a two-line conditional rather than forking, since only the two
service calls differ. Page markup must fork: §9.6 of the UI proposal is explicit that `MudStack`,
`MudCard`, `MudTextField` and friends are deliberately not wrapped.

**No `#if (ui_*)` inside `.razor` files.** Razor plus the `dotnet new` preprocessor is the worst
combination to maintain and would make every adapter change a conflict. Selection is by
`sources[].condition` + `modifiers` in `template.json`, so adding a third adapter one day is a new
directory touching no existing file.

**`GKit.Cli` holds no UI or feature knowledge in code.** A declarative `features.json` inside
`GKit.Templates` maps feature → package, `using`, `Program.cs` snippet, health-check line, with a
parallel `ui` table. Adding an adapter or a capability is a data row, not a command-code edit.

## 6. CLI commands beyond `new`

| Command | What it does | Friction it removes |
|---|---|---|
| `gkit link [--path ../DotnetStack] [GKit.X …]` / `gkit unlink` | Swaps `PackageReference` ⇄ `ProjectReference` in place and adds/removes the projects in the `.slnx` | The commented-out blocks in all three repos; the `HintPath` in `Benati.Rifiuti.Data` |
| `gkit add <feature\|crud\|job\|clirunner>` | Item templates into an existing solution, with the `Program.cs` and health-check wiring applied | The four coordinated edits of §4 |
| `gkit doctor` | Flags `HintPath` references, mixed `GKit.*` versions, missing `version.json`, `Manifest.cs`-less split data layers, and **references to the retired `GKit.MudBlazorExt`** | Friction 1–2 |
| `gkit migrate ui` | Codemod for the UI proposal's §9 breaking table: `CellContext`→`RowContext`, `Color`→`UiColor`, `Icons.Material.*`→`UiIcon`, `GridStateVirtualize`/`GridData`→`GridQuery`/`GridPage`, `QueryFilterExtensions.Where(q, …)`→`s.ApplyFilters(q)`, namespace rename | ~65 mechanical sites across three repos |
| `gkit update [--to latest]` | Aligns `GKit.*` versions through `Directory.Packages.props` | Friction 2 |
| `gkit release [--rid …]` | The shared `release.sh` logic — git height + branch → `build/<Project>.<height>-<branch>.zip` | Friction 3 |

`migrate ui` is new in rev 2 and is arguably the most valuable command in the list: the UI proposal
deliberately did not preserve source compatibility, and the three reference apps are unmigrated.

## 7. Layout

```
GKit.Templates/
  GKit.Templates.csproj          PackageType=Template, IncludeContentInPack, no lib output
  templates/
    gkit-sln/    gkit-app/    gkit-shared/    gkit-data/
    gkit-migrator/    gkit-worker/    gkit-test/
    items/{crud,job,clirunner,opcua-context,plc-context,report}/
  features.json
GKit.Cli/
  GKit.Cli.csproj                PackAsTool, ToolCommandName=gkit
  Commands/{New,Add,Link,Unlink,Doctor,MigrateUi,Update,Release}Command.cs
```

`System.CommandLine` for parsing; `Microsoft.Build` / `XDocument` for the csproj and `.slnx` edits.
`.github/workflows/publish.yaml` already packs the whole solution, so both packages publish with no
CI change.

## 8. Sequencing

| Phase | Work | Outcome |
|---|---|---|
| 0 | `gkit-app` + `gkit-sln`, `--db` / `--ui` / `--host` / `--features` | Validated by regenerating the StuffHR and ErpPalletLink skeletons and diffing against the real ones |
| 1 | `GKit.Cli` with `new`, `link`/`unlink`, `doctor` | Immediately useful on the three existing repos, before any new project exists |
| 2 | `gkit migrate ui` | Unblocks migrating the reference apps onto `GKit.UI.*` |
| 3 | `gkit-shared` / `gkit-data` / `gkit-migrator` / `gkit-worker` / `gkit-test`; `--auth` variants | |
| 4 | Item templates (`gkit-crud` first), `update`, `release` | |

Phase 1 before phase 0 completes is deliberate: `link`/`unlink`/`doctor` pay off on repositories
that exist today and need no templates at all.

## 9. Open questions

| Question | Status |
|---|---|
| Template default culture vs library default | Closed — decision 4: templates default to it-IT *by referencing* `GKit.UI.Localization`; the library default stays neutral English |
| Local accounts as library or template content | Closed — `GKit.Authentication.Simple`, on this branch |
| Does `gkit-app` scaffold both UIs at once, or one | Open — the demo hosts prove two hosts over one `.Shared` is viable, but no reference app needs it. Proposed: one host, `gkit add ui radzen` later |
| `gkit migrate ui` scope | Open — mechanical renames are safe; §9.2 of the UI proposal (method calls in property expressions, e.g. `x => x.MainSite()!.AddressNumber`) cannot be codemodded and should be reported, not rewritten |
| Whether `Test.Repo` and `GKit.Tests` conventions both need templates | Open — `gkit-test` currently models `GKit.Tests` only |
