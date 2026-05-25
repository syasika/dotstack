---
name: fix-build-errors
description: Diagnose and fix common .NET build failures in dotstack. Covers missing ProjectReferences, central package version errors, .slnx registration, RootNamespace mismatches, and dependency cycle violations. Use when build fails, dotnet build/restore errors, or user reports compilation errors in this repo.
---

# Fix Build Errors — dotstack

## Quick start

```bash
dotnet build 2>&1 | head -60
```

Read error output. Match symptom → fix below.

## Common symptoms

### Missing ProjectReference

Error: `CS0246: The type or namespace 'DotStack.Core' could not be found`

Dependency graph: `Cli → Core`, `Cli → Tui`, `Tui → Core`. Tests reference their source project. No other edges allowed.

Fix: add missing `<ProjectReference>` in the consumer's `.csproj`.

### Central package version not found

Error: `NU1012: Package version not found for 'AWSSDK.Xyz'`

Fix: add `<PackageVersion Include="AWSSDK.Xyz" Version="x.y.z" />` to `Directory.Packages.props`. `<PackageReference>` in `.csproj` must NOT include a `Version` attribute (central management).

### Project not in solution

Error: `MSB3202: The project file "...csproj" was not found`

Fix: add `<Project Path="{relative-path}" />` to `dotstack.slnx`. Test projects go inside `<Folder Name="/test/">`.

### RootNamespace mismatch

Runtime error accessing types from another project.

Fix: check `.csproj` `<RootNamespace>` matches expected namespace. Map:
- `dotstack.Core` → `DotStack.Core`
- `dotstack.Cli` → `DotStack.Cli`
- `dotstack.Tui` → `DotStack.Tui`
- Test projects → `DotStack.{Project}.Tests`

### Dependency cycle

Error: `NETSDK1155: Circular dependency detected`

Fix: dependency graph is one-way only — `Cli → Core → nothing`, `Cli → Tui → Core`. Never add `Core → Cli` or `Core → Tui`.

### Wrong TargetFramework

Fix: all projects inherit `net10.0` from `Directory.Build.props` — do NOT override `<TargetFramework>` in individual `.csproj`s unless deliberate.

## Workflow

1. Run `dotnet build` capture full output
2. Identify error code (CS0246, NU1012, MSB3202, NETSDK1155)
3. Apply matching fix above
4. Rebuild. If new error appears, recurse.

## Diagnostics

- `dotnet build --verbosity d` — detailed build log for obscure errors
- `dotnet restore` — resolve NuGet issues separately
- Check `.csproj` for missing `<PackageReference>` when types from AWSSDK or NuGet packages are unresolved
