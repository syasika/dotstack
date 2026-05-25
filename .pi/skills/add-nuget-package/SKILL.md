---
name: add-nuget-package
description: Add a new NuGet dependency to dotstack with correct central version management (Directory.Packages.props), project-level reference, and restore. Use when adding a new NuGet package or when build fails with NU1012 referencing a missing package version.
---

# Add NuGet Package to dotstack

## Quick start

Given `AWSSDK.DynamoDBv2` needed by `dotstack.Core`:

1. Add version to `Directory.Packages.props`
2. Add reference to `dotstack.Core/dotstack.Core.csproj`
3. `dotnet restore`

## Workflow

### 1. Find the package version

Check latest stable on NuGet.org. For AWS SDK packages, use version compatible with existing ones (see `Directory.Packages.props` — currently `AWSSDK.Core` 4.x).

### 2. Add central version

`Directory.Packages.props` has `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`. Add inside `<ItemGroup>`:

```xml
<PackageVersion Include="AWSSDK.DynamoDBv2" Version="4.0.0.1" />
```

Do NOT add to any other `<ItemGroup>`.

### 3. Add reference to project

Edit the consuming `.csproj`. Add inside `<ItemGroup>`:

```xml
<PackageReference Include="AWSSDK.DynamoDBv2" />
```

No `Version` attribute — central management supplies it.

### 4. Pick correct project

| Package belongs in | Why |
|---|---|
| `dotstack.Core` | AWS SDK, Docker.DotNet — shared service logic |
| `dotstack.Cli` | Spectre.Console, Spectre.Console.Cli — command parsing |
| `dotstack.Tui` | Spectre.Console — TUI rendering (already in Cli) |
| Any test project | Microsoft.NET.Test.Sdk, xunit.v3, Shouldly, FakeItEasy |

Test projects also need `<PackageReference Include="FakeItEasy" />` and `<PackageReference Include="Shouldly" />` if mocking/assertions needed.

### 5. Restore & build

```bash
dotnet restore
dotnet build
```

If `NU1012` error: package version missing from `Directory.Packages.props`.
If `CS0246`: verify namespace matches package docs.
