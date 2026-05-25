---
name: add-unit-tests
description: Generate unit test skeletons for dotstack .NET source files (xUnit v3, Shouldly, FakeItEasy). Maps source to test projects, handles AWS client mocking via IAmazon* interface extraction. Use when adding tests or user mentions writing unit tests for dotstack.
---

# Add Unit Tests to dotstack

## Quick start

Given `dotstack.Core/S3/S3Operations.cs`:
1. Refactor `AmazonS3Client` params to `IAmazonS3` (prereq for mocking)
2. Create `test/dotstack.Core.Tests/S3OperationsTests.cs`
3. Generate `[Fact]` stubs following existing patterns

## Prereqs

Read CONTEXT.md for domain language. Read docs/adr/0001-multi-project-architecture.md for project layering. Read existing test files under test/ for style.

## Source-to-test mapping

Source file                               | Test project                  | Test namespace
------------------------------------------|-------------------------------|-------------------
`dotstack.Core/**/*.cs`                   | `test/dotstack.Core.Tests/`   | `DotStack.Core.Tests`
`dotstack.Cli/**/*.cs`                    | `test/dotstack.Cli.Tests/`    | `DotStack.Cli.Tests`
`dotstack.Tui/**/*.cs`                    | `test/dotstack.Tui.Tests/`    | `DotStack.Tui.Tests`

Test file name = `{SourceClassName}Tests.cs`. E.g. `S3Operations.cs` → `S3OperationsTests.cs`.

## AWS client mock workflow

Operations take concrete `Amazon{Service}Client` params. To mock:

### 1. Refactor source to use interface

Change `AmazonS3Client` → `IAmazonS3`, `AmazonSQSClient` → `IAmazonSQS`, etc.

| Concrete client | Interface | Namespace |
|---|---|---|
| `AmazonS3Client` | `IAmazonS3` | `Amazon.S3` |
| `AmazonSimpleSystemsManagementClient` | `IAmazonSimpleSystemsManagement` | `Amazon.SimpleSystemsManagement` |
| `AmazonSQSClient` | `IAmazonSQS` | `Amazon.SQS` |
| `AmazonSimpleNotificationServiceClient` | `IAmazonSimpleNotificationService` | `Amazon.SimpleNotificationService` |

Update callers (Cli, Tui) to pass interface too.

### 2. Mock in tests

```csharp
var fake = A.Fake<IAmazonS3>();
A.CallTo(() => fake.ListBucketsAsync(A<CancellationToken>._))
    .Returns(new ListBucketsResponse { Buckets = ... });
```

Use `A<CancellationToken>._` for `CancellationToken` params. Use `A<T>._` for other params you don't care about matching exactly.

For methods that return `Task<T>`, `.Returns()` accepts `T` (framework wraps it). For methods returning `Task`, use `.Returns(Task.CompletedTask)`.

Verify calls with `.MustHaveHappened()` or `.MustHaveHappenedOnceExactly()`.

## Examples

**Static utility:**
```csharp
[Fact]
public void IsConnectionError_connection_refused_returns_true()
{
    var ex = new InvalidOperationException("connection refused");
    AwsExceptionHelper.IsConnectionError(ex).ShouldBeTrue();
}
```

**Operation with mocked client:**
```csharp
[Fact]
public async Task ListBucketsAsync_returns_bucket_names()
{
    var fake = A.Fake<IAmazonS3>();
    var resp = new ListBucketsResponse
    {
        Buckets = [new S3Bucket { BucketName = "b1" }, new S3Bucket { BucketName = "b2" }]
    };
    A.CallTo(() => fake.ListBucketsAsync(A<CancellationToken>._)).Returns(resp);
    var result = await S3Operations.ListBucketsAsync(fake);
    result.ShouldBe(["b1", "b2"]);
}
```

## Checklist

- [ ] Refactor AWS clients to `IAmazon*` interfaces in source + callers
- [ ] Test file in correct project dir
- [ ] Namespace matches test project
- [ ] `[Fact]` naming: `{Method}_when_{condition}_returns_{result}`
- [ ] CLI tests: test operation methods, not command handlers
- [ ] TUI tests: test helper methods, not live rendering
- [ ] Build + run: `dotnet test`
