# GKit.Templates

`dotnet new` templates for the GKit suite.

```sh
dotnet new install GKit.Templates
```

| Template | Short name | What it emits |
|---|---|---|
| Solution root | `gkit-sln` | `.slnx`, `Directory.Build.props`, `Directory.Packages.props` (CPM), `version.json`, `release.sh`, `resetdb.sh`, `docker-compose.yml`, `.gitignore`, CI workflow |
| Blazor application | `gkit-app` | Blazor Server host: `Application.Wrap`, Serilog, health checks, localization, an EF `DbContextFactory`, an optional UI adapter and optional capability packs |
| Shared domain library | `gkit-shared` | UI neutral model, validators and query factories; references neither adapter |
| Data layer | `gkit-data` | `X.Data` plus an `X.Data.EF.<Provider>` sibling owning `Migrations/` and `Manifest.cs` |
| Legacy migrator | `gkit-migrator` | Console host on `GKit.DbMigration`, with a resumable mappings store |
| Worker service | `gkit-worker` | Headless host: Quartz, MQTT, SMTP, Telegram, no Blazor |
| Test project | `gkit-test` | xunit + `SqliteFixture` + bUnit, on the `GKit.Tests` conventions |

Item templates, added into an existing project with `gkit add`:

| Template | Short name | What it emits |
|---|---|---|
| CRUD entity | `gkit-crud` | Model, validator and query factory (UI neutral) plus grid, form, dialog and page (per adapter). `--part shared\|ui\|all` picks the half |
| Quartz job | `gkit-job` | An `IJob` with `[CronSchedule]` or `[TimeSpanSchedule]` |
| Command line runner | `gkit-clirunner` | An `ICommandLineRunner` on the project's `CommandLineRunnerBase` |

Prefer driving these through [`GKit.Cli`](https://www.nuget.org/packages/GKit.Cli): it expands the
comma separated `--features` list and reconciles package versions with Central Package Management.

## Options

```
--ui            mudblazor | radzen | none                     (default mudblazor)
--db            sqlserver | npgsql | mysql | sqlite | none    (default sqlserver)
--auth          none | simple | activedirectory | entra
--host          windows-service | systemd | docker
--features      quartz opcua plc mqtt pdf rentri reporting settings smartcard smtp telegram
--culture       default it-IT
--health --httpsCerts --cliRunners                            (all true by default)
--gkitVersion   default 0.1.*
```

`dotnet new` wants one `--features` flag per value:

```sh
dotnet new gkit-app -n Acme.Erp --features quartz --features reporting
gkit new app Acme.Erp --features quartz,reporting          # equivalent
```

## How the UI axis is handled

The two adapters ship identical component names in distinct namespaces, so switching is a package
swap, one `@using` and one service call. The template therefore keeps `Program.cs` shared and forks
only `_Imports.razor`, `App.razor`, `Components/Layout` and the page markup, through separate
content roots selected by a `sources` condition rather than `#if` inside `.razor` files.

Adding a third adapter is a new directory that touches no existing file.

## features.json

`features.json` is the manifest **GKit.Cli** embeds. It maps every capability onto the four edits
that must stay in step - package reference, `using`, the `Add...` call and the health check - plus
the UI, database and authentication tables and the rename list behind `gkit migrate ui`.

The templates encode the same knowledge as preprocessor conditions, because `dotnet new` cannot
read the manifest; the manifest is what lets the tool make the same edits to a solution that
already exists.

## Known limitation

`--auth entra` wires `Microsoft.Identity.Web` and emits the plain router. The account pages and the
`AuthorizeRouteView` variant are only generated for the cookie based modes (`simple`,
`activedirectory`), which are what the reference applications use.
