---
name: nuget-publish
description: Bump the VGrid.Editor package version and publish it to nuget.org directly from the local machine (no GitHub Actions/CI). Use when the user asks to release, publish, or push a new VGrid.Editor version to NuGet.
---

# Publish VGrid.Editor to nuget.org

This repo publishes `VGrid.Editor` straight from a local machine — there is no CI
workflow for this (a GitHub Actions workflow existed once but was removed; do not
recreate one unless the user explicitly asks).

## Prerequisites

- `NUGET_API_KEY` must already be set as a local environment variable. If it isn't
  set, stop and ask the user how they want to supply the key rather than guessing.

## Steps

1. **Bump the version** in `Directory.Build.props` (repo root), under the
   `VGrid.Editor` property group:
   ```xml
   <Version Condition="'$(Version)' == ''">X.Y.Z</Version>
   ```
   Confirm the new version number with the user if it's not obvious (patch vs.
   minor vs. major) — don't just increment blindly.

2. **Restore, build, test** from the repo root:
   ```bash
   dotnet restore VGrid.sln
   dotnet build VGrid.sln -c Release --no-restore
   dotnet test tests/VGrid.Tests/VGrid.Tests.csproj -c Release --no-build
   ```
   All tests must pass before packing.

3. **Pack**:
   ```bash
   mkdir -p artifacts/packages
   dotnet pack src/VGrid.Editor/VGrid.Editor.csproj -c Release --no-build \
     -p:Version=X.Y.Z -o artifacts/packages
   ```

4. **Push to nuget.org**:
   ```bash
   dotnet nuget push artifacts/packages/VGrid.Editor.X.Y.Z.nupkg \
     --source "https://api.nuget.org/v3/index.json" \
     --api-key "$NUGET_API_KEY" --skip-duplicate
   ```
   This step publishes publicly and is irreversible (only unlisting is possible
   afterward) — treat it as the point of no return in this flow.

5. **Commit** the version bump in `Directory.Build.props` (ask the user first,
   per standard commit policy). Do not push to the git remote unless asked.

## Notes

- `PackageId` is `VGrid.Editor`; only that project is packed, not the app itself
  (`src/VGrid/VGrid.csproj`, `OutputType=Library`... actually `VGrid.csproj` is the
  WPF app, `VGrid.Editor.csproj` is the packable UserControl library — pack only
  the latter).
- Don't add a GitHub Actions publish workflow back in — this project intentionally
  publishes manually from a local machine.
