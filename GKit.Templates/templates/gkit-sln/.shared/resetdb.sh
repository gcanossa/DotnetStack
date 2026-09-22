#!/bin/sh
# Drops the development database and rebuilds it from a single Initial migration.
# Destructive: development only.
set -e

PROJECT="GKit.Sln1"
CONTEXT="ApplicationDbContext"

dotnet ef database update 0 -p "./$PROJECT" -c "$CONTEXT" -- --environment Development
dotnet ef migrations remove -p "./$PROJECT" -c "$CONTEXT" -- --environment Development || true
dotnet ef migrations add Initial -p "./$PROJECT" -c "$CONTEXT" -- --environment Development
dotnet ef database update -p "./$PROJECT" -c "$CONTEXT" -- --environment Development
