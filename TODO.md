# dotstack.Cli — Test Coverage Plan

## Goal
Take CLI project from **0.1% → 90%+** line coverage via DI container refactor + comprehensive unit tests.

## Status

### ✅ Phase 0 — DI Infrastructure (COMPLETE)

**Packages added:**
- `Microsoft.Extensions.DependencyInjection` 10.0.0 → `dotstack.Cli`, `dotstack.Cli.Tests`
- `Spectre.Console.Testing` 0.54.0 → `dotstack.Cli.Tests`

**New files in `dotstack.Cli/`:**

| File | Purpose |
|------|---------|
| `Abstractions/IAwsClientFactory.cs` | Interface: `CreateS3Client`, `CreateSsmClient`, `CreateSqsClient`, `CreateSnsClient` |
| `Abstractions/IDockerClientFactory.cs` | Interface: `CreateClient()` → `IDockerClient` |
| `Infrastructure/AwsClientFactoryWrapper.cs` | Default impl — delegates to static `AwsClientFactory` |
| `Infrastructure/DockerClientFactory.cs` | Default impl — `new DockerClientConfiguration().CreateClient()` |
| `Infrastructure/TypeRegistrar.cs` | `ITypeRegistrar` backed by `ServiceCollection` |
| `Infrastructure/TypeResolver.cs` | `ITypeResolver` backed by `ServiceProvider` |
| `Infrastructure/ConfigPrompter.cs` | Extracted from `InitCommand` — interactive prompt logic |
| `Infrastructure/ContainerInitializer.cs` | Extracted from `InitCommand` — Docker pull/create/start |

**Modified files:**

| File | Change |
|------|--------|
| `Program.cs` | Builds `ServiceCollection`, registers `IAnsiConsole`, `IAwsClientFactory`, `IDockerClientFactory`, passes `TypeRegistrar` to `CommandApp` |
| `S3Commands.cs` | All 4 commands inject `IAnsiConsole` + `IAwsClientFactory`. Replace `AnsiConsole.MarkupLine` → `_console.MarkupLine`. |
| `SsmCommands.cs` | All 4 commands inject `IAnsiConsole` + `IAwsClientFactory`. |
| `SqsCommands.cs` | All 5 commands inject `IAnsiConsole` + `IAwsClientFactory`. |
| `SnsCommands.cs` | All 4 commands inject `IAnsiConsole` + `IAwsClientFactory`. |
| `ContainerCommands.cs` | All 4 commands inject `IAnsiConsole` + `IDockerClientFactory`. |
| `InitCommand.cs` | Injects deps, delegates to `ConfigPrompter` + `ContainerInitializer`. Internal ctor for test injection. |
| `BrowseCommand.cs` | Unchanged — no static calls. |

---

### ✅ Phase 1 — Write Tests (COMPLETE)

**Total: 74 tests — all passing.**

| Test file | Tests | What it covers |
|-----------|-------|----------------|
| `EndpointSettingsTests.cs` | 4 | Default URL, verbose default, property setters |
| `VerboseActivityExporterTests.cs` | 6 | Disabled/enabled, success/error activity, null tag, multi-batch |
| `S3CommandsHelpersTests.cs` | 11 | `StripS3Prefix`, `IsS3Path`, `ParseS3Path` edge cases |
| `S3CommandsTests.cs` | 15 | Ls (empty, with buckets, with objects, folders, prefix), Mb, Rb (normal, force), Cp (download, upload, invalid src/dest, both local, both s3) |
| `SsmCommandsTests.cs` | 7 | Ls (empty, has params), Get, Put (default type, custom type), Rm |
| `SqsCommandsTests.cs` | 8 | Ls (empty, has queues), Create, Rm, Send, Recv (has messages, no messages) |
| `SnsCommandsTests.cs` | 6 | Ls (empty, has topics), Create, Rm, Publish |
| `ContainerCommandsTests.cs` | 11 | Status (running, stopped, not-found, not-initialized), Start (normal, not-initialized), Stop (normal, not-initialized), Remove (normal, force, not-initialized) |
| `InitCommandTests.cs` | 7 | Already running, exited (auto-start), paused (unpause), unexpected state, config-exists-but-not-found, no-config full setup |
| `BrowseCommandTests.cs` | 2 | Type validation, instantiation |

**Production code changes during testing:**

| File | Change | Reason |
|------|--------|--------|
| `S3Commands.cs` | `StripS3Prefix`, `IsS3Path`, `ParseS3Path` changed from `private` → `internal` | Enable unit testing of pure helper functions |
| `ConfigPrompter.cs` | `PromptSetup()` changed from `public` → `public virtual` | Enable FakeItEasy mocking in InitCommand tests |
| `ContainerInitializer.cs` | `EnsureContainer()` changed from `public` → `public virtual` | Enable FakeItEasy mocking in InitCommand tests |
| `SqsCommands.cs` | Message ID and body now use `.EscapeMarkup()` | Real bug: raw user data in markup output caused "Unbalanced markup stack" / "Could not find color or style" |
| `dotstack.Cli.csproj` | Added `<InternalsVisibleTo>` via `AssemblyInfo.cs` | Test access to `VerboseActivityExporter` and other internal types |
| `dotstack.Cli.Tests.csproj` | Added `Spectre.Console.Cli` reference | Required for `Command<T>` base class resolution |
| `Directory.Packages.props` | `Spectre.Console.Testing` 0.54.0 → 0.54.1-alpha.0.86 | Compatibility with `Spectre.Console` 0.55.x (`WriteAnsi` method) |

#### Infra changes

- Added `TestHelpers.cs` — `FakeRemainingArguments`, `TestHelpers.CreateContext()`, `CommandExtensions.Execute()` helper
- Added `Properties/AssemblyInfo.cs` with `[CollectionBehavior(DisableTestParallelization = true)]` — HOME env var manipulation requires sequential test execution

#### Coverage gap analysis

- Edge cases: empty responses, S3 path edge cases ✓ (covered)
- Error paths: Docker container not found, invalid S3 args ✓ (covered)
- Cancellation: token cancellation in each command — still a gap (needs integration-style tests)
- Branch coverage: every `if/else`, `switch` case, `??` coalesce — largely covered

---

### Test patterns

**Production DI wiring (Program.cs):**
```csharp
var services = new ServiceCollection();
services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
services.AddSingleton<IAwsClientFactory, AwsClientFactoryWrapper>();
services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
```

**Unit test pattern (xUnit v3 + FakeItEasy + Spectre.Console.Testing):**
```csharp
public class S3CommandsTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly IAwsClientFactory _factory;
    private readonly IAmazonS3 _s3;

    public S3CommandsTests()
    {
        _console = new TestConsole();
        _s3 = A.Fake<IAmazonS3>();
        _factory = A.Fake<IAwsClientFactory>();
        A.CallTo(() => _factory.CreateS3Client(A<string>._)).Returns(_s3);
    }

    public void Dispose() => _console.Dispose();

    [Fact]
    public async Task LsCommand_no_buckets_shows_empty_message()
    {
        A.CallTo(() => _s3.ListBucketsAsync(A<CancellationToken>._))
            .Returns(new ListBucketsResponse());
        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = await cmd.ExecuteAsync(context, new LsSettings());
        result.ShouldBe(0);
        _console.Output.ShouldContain("No buckets");
    }
}
```

**Existing Core test patterns to follow:**
- Test project: `test/dotstack.Cli.Tests/`
- Namespace: `DotStack.Cli.Tests`
- Framework: xUnit v3 (`[Fact]` async where possible)
- Assertions: `Shouldly`
- Mocking: `FakeItEasy` (mock `IAmazon*` interfaces, not concrete clients)
- Naming: `{Method}_when_{condition}_returns_{result}`
