# GKit.Cli

Command line companion to the GKit suite. Install as a global tool:

```sh
dotnet tool install -g GKit.Cli
dotnet new install GKit.Templates
```

`gkit new` is a thin wrapper over `dotnet new`: the templates in **GKit.Templates** stay the single
source of truth. Everything else is lifecycle work `dotnet new` cannot express.

## Scaffolding

```sh
gkit new sln Acme.Erp --db npgsql --rid linux-arm64
cd Acme.Erp
gkit new app Acme.Erp --ui mudblazor --db npgsql --auth simple --features quartz,reporting
```

`--features` accepts the comma separated form the documentation uses; the tool expands it into the
repeated flags `dotnet new` expects. When the solution manages package versions centrally, versions
emitted by the project template are moved into `Directory.Packages.props` automatically - a
template cannot see its sibling, so the reconciliation happens here.

## Adding to an existing solution

```sh
gkit add quartz                       # package + using + AddGKitQuartz() + health check + UseGKitQuartz()
gkit add crud -n Vehicle --ui mudblazor --part all \
  --sharedNamespace Acme.Erp.Data --hostNamespace Acme.Erp.Components
gkit add job -n NightlyJob --schedule "0 0 2 * * ?"
gkit add clirunner -n SeedCommandLineRunner --flag --seed
```

A capability is four edits that must stay in step - package reference, `using`, the `Add...` call
and the health check - so they are applied together from `features.json`. The operation is
idempotent, and the version comes from whatever the solution already pins GKit at, so adding a
capability later cannot introduce the drift `doctor` reports as `GKIT003`.

## Working against a local GKit checkout

```sh
gkit link --path ../DotnetStack    # PackageReference -> ProjectReference, and into the solution
gkit unlink                        # exactly back again
```

`link` records what it replaced in `.gkit-link.json`, so `unlink` restores the original version
rather than guessing. Restrict the scope with `--only GKit.Quartz,GKit.RENTRI`, and skip the
solution file with `--no-solution`.

## Diagnosing

```sh
gkit doctor
```

| Code | Meaning |
|---|---|
| `GKIT001` | `<Reference>` with a `HintPath` into someone's `bin/Debug` |
| `GKIT002` | A retired package, with its replacement |
| `GKIT003` | One GKit package pinned to different versions in different projects |
| `GKIT004` | No `Directory.Packages.props` while several projects pin GKit independently |
| `GKIT005` | No `version.json`, so versioning falls back to `0.0.x` |
| `GKIT006` | A commented-out `ProjectReference` block, toggled by hand |
| `GKIT007` | A split data layer whose provider project has no `Manifest.cs` |

Exit code is 1 when any error is reported, so it drops into CI unchanged.

## Migrating to GKit.UI.\*

```sh
gkit migrate ui           # report
gkit migrate ui --apply   # write
```

Applies the mechanical renames from section 9 of `docs/ui-abstraction-proposal.md`
(`CellContext` → `RowContext`, `Color` → `UiColor`, `Icons.Material.*` → `UiIcon`,
`GridStateVirtualize`/`GridData` → `GridQuery`/`GridPage`, and the package and namespace rename).

Anything needing judgement is reported and left alone - a grid column whose sort expression calls a
method would compile after a blind rewrite and then fail at runtime under Dynamic LINQ.

## Versions and releases

```sh
gkit update --dry-run     # report version drift
gkit update --to 0.1.4    # align every GKit pin
gkit release --rid linux-arm64
```

`release` publishes single-file and framework-dependent, then zips into
`build/<Project>.<git height>-<branch>.zip` - the behaviour the reference applications each carried
a drifting copy of in their own `release.sh`.
