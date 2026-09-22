#!/bin/sh
# Publishes each project listed in PROJECTS as a single-file, framework-dependent build and
# drops one zip per project into ./build, named <Project>.<git height>-<branch>.zip.
#
# This replaces the copy of this script that each GKit application used to carry: those copies
# drifted, and two of the three were missing the mkdir guard below.
set -e

PROJECTS="GKit.Sln1"
RUNTIME="GKIT-RID"
FRAMEWORK="net10.0"

CUR_BRANCH=$(git rev-parse --abbrev-ref HEAD)
GIT_HEIGHT=$(git rev-list --count HEAD)
ROOT=$(pwd)

mkdir -p "$ROOT/build"
rm -f "$ROOT"/build/*.zip

for PROJECT in $PROJECTS; do
  echo "Publishing $PROJECT for $RUNTIME"

  cd "$ROOT/$PROJECT"
  dotnet clean --verbosity quiet
  dotnet publish -c Release --runtime "$RUNTIME" -p:PublishSingleFile=true --no-self-contained

  cd "bin/Release/$FRAMEWORK/$RUNTIME/publish"
  zip -r "$ROOT/build/$PROJECT.$GIT_HEIGHT-$CUR_BRANCH.zip" ./*
  cd "$ROOT"
done

echo "Artifacts in $ROOT/build"
