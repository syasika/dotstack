---
name: add-aws-service
description: "Add a new AWS service (e.g. DynamoDB, Lambda, Kinesis) to the dotstack .NET project across all layers: Core operations, CLI commands, TUI dashboard, package config, and tests. Use when extending dotstack's AWS service coverage or adding a new AWS service integration."
---

# Add AWS Service to dotstack

Add a new AWS service to the dotstack .NET project. Each service touches 5 layers: `dotstack.Core/<Service>/`, `dotstack.Core/Aws/AwsClientFactory.cs`, `dotstack.Cli/Commands/`, `dotstack.Cli/Program.cs`, and `dotstack.Tui/BrowseDashboard.cs`.

**Prereqs**: Understand dotstack domain language (CONTEXT.md), existing services (S3, SSM, SQS, SNS).

## Workflow

### 1. Add NuGet package

- Add `<PackageVersion>` to `Directory.Packages.props`
- Add `<PackageReference>` to `dotstack.Core/dotstack.Core.csproj`

**AWS SDK NuGet naming**: `AWSSDK.{Service}` — e.g. `AWSSDK.DynamoDBv2`, `AWSSDK.Lambda`.

### 2. Create Core operations

- Create `dotstack.Core/{ServiceName}/{ServiceName}Operations.cs`
- Define domain records for list items
- One static class with `async` methods, each wrapped in `try/catch` calling `AwsExceptionHelper.ToFriendlyError(ex, "{SERVICE}")`
- Follow existing patterns: SnsOperations (simple CRUD) or S3Operations (more operations)

### 3. Add client factory

- Add config class + public factory method in `dotstack.Core/Aws/AwsClientFactory.cs`
- Dummy credentials `new BasicAWSCredentials("miniaws", "miniaws")`
- `RegionEndpoint.USEast1`, `ServiceURL = endpoint`, `MaxErrorRetry = 1`

### 4. Create CLI commands

- Create `dotstack.Cli/Commands/{ServiceName}Commands.cs`
- Nested settings classes extending `EndpointSettings`
- Nested command classes, each `Command<TSettings>` calling `AwsClientFactory.Create{Service}Client`
- Standard output patterns: `AnsiConsole.MarkupLine` with Spectre.Console markup

### 5. Register in Program.cs

- Add `config.AddBranch("{name}", ...)` block in `dotstack.Cli/Program.cs`

### 6. Add TUI dashboard support

- Add value to `ServiceMode` enum in `dotstack.Tui/BrowseDashboard.cs`
- Add state fields, client field, refresh/delete methods
- Wire into `SwitchMode`, `HandleKey`, `HandleEnter`, `HandleRefresh`, `HandleDelete`
- Add tab line in `GetRenderable`, render method, help text
- Add `Dispose` call

### 7. Add tests

- `test/dotstack.Core.Tests/AwsClientFactoryTests.cs` — add factory test
- Core operation tests follow the pattern in existing tests

See [REFERENCE.md](REFERENCE.md) for exact code templates and file diffs.
