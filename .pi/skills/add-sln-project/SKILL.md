---
name: add-sln-project
description: Add a new project to the dotstack .NET solution with correct .slnx entry, .csproj scaffolding, RootNamespace, and optional matching test project. Use when adding a new assembly to the solution or when user says to create a new project in this repo.
---

# Add Project to dotstack Solution

## Quick start

Create new project + add to solution + wire references:

1. Create `.csproj`
2. Add to `dotstack.slnx`
3. Add `ProjectReferences`
4. Build

## Workflow

### 1. Create .csproj

New source project (`dotstack.Foo/`):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>DotStack.Foo</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\dotstack.Core\dotstack.Core.csproj" />
  </ItemGroup>
</Project>
```

New test project (`test/dotstack.Foo.Tests/`):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>DotStack.Foo.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="FakeItEasy" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\dotstack.Foo\dotstack.Foo.csproj" />
  </ItemGroup>
</Project>
```

Do NOT set `TargetFramework`, `ImplicitUsings`, `Nullable` — inherited from `Directory.Build.props` (`net10.0`, enabled, enabled).

### 2. Add to .slnx

Edit `dotstack.slnx`. Source projects at root level:
```xml
<Project Path="dotstack.Foo/dotstack.Foo.csproj" />
```

Test projects inside Folder:
```xml
<Folder Name="/test/">
  <Project Path="test/dotstack.Foo.Tests/dotstack.Foo.Tests.csproj" />
</Folder>
```

### 3. Wire ProjectReferences

Follow dependency rules: `Foo → Core` allowed. `Foo → Cli` or `Foo → Tui` discouraged (architectural violation). Test projects reference their source project only.

### 4. Namespace convention

| Project dir | RootNamespace |
|---|---|
| `dotstack.Foo` | `DotStack.Foo` |
| `dotstack.Foo.Bar` | `DotStack.Foo.Bar` |
| `test/dotstack.Foo.Tests` | `DotStack.Foo.Tests` |

### 5. Build & verify

```bash
dotnet build
```
