# GKit.Sln1

Scaffolded with `gkit new sln`.

## Layout

| Path | Purpose |
|---|---|
| `Directory.Build.props` | Nerdbank.GitVersioning for every project |
| `Directory.Packages.props` | Central Package Management — package versions live here, not in the csproj files |
| `version.json` | Version height configuration |
| `release.sh` | Publishes each project in `PROJECTS` and zips it into `build/` as `<Project>.<height>-<branch>.zip` |
| `resetdb.sh` | Drops and rebuilds the development database from a single `Initial` migration |
| `docker-compose.yml` | Development database |

## Day to day

```sh
docker compose up -d          # start the development database
dotnet run --project GKit.Sln1
./release.sh                  # publish + zip into build/
```

## Working against a local GKit checkout

While developing GKit itself, swap the package references for project references:

```sh
gkit link --path ../DotnetStack     # PackageReference -> ProjectReference
gkit unlink                         # and back again
```

`gkit doctor` reports version drift, references to retired packages and other things that
tend to rot in these solutions.
