# Add AWS Service — Reference

File-by-file code templates for adding a new AWS service. Variables in `{braces}` are placeholders.

## 1. Directory.Packages.props

Add centralized package version:

```xml
<PackageVersion Include="AWSSDK.{Service}" Version="{x.y.z}" />
```

## 2. dotstack.Core/dotstack.Core.csproj

Add package reference (uses centralized version from step 1):

```xml
<PackageReference Include="AWSSDK.{Service}" />
```

## 3. dotstack.Core/Aws/AwsClientFactory.cs

Add config class + factory method:

```csharp
private static Amazon{Service}Config Create{Service}Config(string endpoint) => new()
{
    RegionEndpoint = RegionEndpoint.USEast1,
    ServiceURL = endpoint,
    MaxErrorRetry = 1
};

public static Amazon{Service}Client Create{Service}Client(string endpoint) =>
    new(Credentials, Create{Service}Config(endpoint));
```

Add `using` imports at top if new namespace.

## 4. dotstack.Core/{Service}/{Service}Operations.cs

Full pattern (CRUD example — adapt operations to the service):

```csharp
using Amazon.{Service};
using Amazon.{Service}.Model;
using DotStack.Core.Aws;

namespace DotStack.Core.{Service};

public record {Service}Item(string Name, string Arn);

public static class {Service}Operations
{
    public static async Task<List<{Service}Item>> List{Service}Async(
        Amazon{Service}Client client, CancellationToken ct = default)
    {
        try
        {
            var items = new List<{Service}Item>();
            var request = new List{Service}Request();

            List{Service}Response response;
            do
            {
                response = await client.List{Service}Async(request, ct);
                foreach (var t in response.{Service}List)
                    items.Add(new {Service}Item(t.{Name}, t.{Arn}));

                request.NextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(response.NextToken));

            return items;
        }
        catch (Exception ex)
        {
            throw AwsExceptionHelper.ToFriendlyError(ex, "{SERVICE}");
        }
    }

    public static async Task<{Service}Item> Create{Service}Async(
        Amazon{Service}Client client, string name,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Create{Service}Request { Name = name };
            var response = await client.Create{Service}Async(request, ct);
            return new {Service}Item(name, response.{Arn});
        }
        catch (Exception ex)
        {
            throw AwsExceptionHelper.ToFriendlyError(ex, "{SERVICE}");
        }
    }

    public static async Task Delete{Service}Async(
        Amazon{Service}Client client, string arn,
        CancellationToken ct = default)
    {
        try
        {
            var request = new Delete{Service}Request { {Arn} = arn };
            await client.Delete{Service}Async(request, ct);
        }
        catch (Exception ex)
        {
            throw AwsExceptionHelper.ToFriendlyError(ex, "{SERVICE}");
        }
    }

    public static string Extract{Service}Name(string arn)
    {
        var parts = arn.Split(':');
        return parts[^1];
    }
}
```

## 5. dotstack.Cli/Commands/{Service}Commands.cs

```csharp
using Spectre.Console;
using Spectre.Console.Cli;
using DotStack.Core.Aws;
using DotStack.Core.{Service};

namespace DotStack.Cli.Commands;

public static class {Service}Commands
{
    public sealed class ArnSettings : EndpointSettings
    {
        [CommandArgument(0, "<{arn-name}>")]
        public string {Arn} { get; set; } = "";
    }

    public sealed class NameSettings : EndpointSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";
    }

    public class LsCommand : Command<EndpointSettings>
    {
        protected override int Execute(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.Create{Service}Client(settings.EndpointUrl);
            var items = {Service}Operations.List{Service}Async(client, cancellationToken).GetAwaiter().GetResult();
            if (items.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No items.[/]"); return 0; }
            AnsiConsole.MarkupLine($"[bold white on #0066CC] {Service} ({items.Count}) [/]");
            foreach (var i in items) AnsiConsole.MarkupLine($"  [bold #0044CC]{i.Name}[/]");
            return 0;
        }
    }

    public class CreateCommand : Command<NameSettings>
    {
        protected override int Execute(CommandContext context, NameSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.Create{Service}Client(settings.EndpointUrl);
            var item = {Service}Operations.Create{Service}Async(client, settings.Name, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] {Service} '[bold]{item.Name}[/]' created");
            return 0;
        }
    }

    public class RmCommand : Command<ArnSettings>
    {
        protected override int Execute(CommandContext context, ArnSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.Create{Service}Client(settings.EndpointUrl);
            {Service}Operations.Delete{Service}Async(client, settings.{Arn}, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] {Service} deleted");
            return 0;
        }
    }
}
```

## 6. dotstack.Cli/Program.cs

Add branch registration:

```csharp
config.AddBranch("{name}", {svc} =>
{
    {svc}.SetDescription("Manage {Name} resources");
    {svc}.AddCommand<{Service}Commands.LsCommand>("ls");
    {svc}.AddCommand<{Service}Commands.CreateCommand>("create");
    {svc}.AddCommand<{Service}Commands.RmCommand>("rm");
});
```

## 7. dotstack.Tui/BrowseDashboard.cs

Each new service requires edits in up to 10 locations. Follow the SNS pattern (simplest — list-only view).

### 7a. ServiceMode enum

Add value:

```csharp
public enum ServiceMode { S3, Ssm, Sqs, Sns, {Service} }
```

### 7b. Fields

```csharp
private readonly Amazon{Service}Client _{service}Client;
// State
private List<{Service}Item> _{service}Items = [];
private string _{service}Error = "";
```

### 7c. Constructor

```csharp
_{service}Client = (Amazon{Service}Client)(object)AwsClientFactory.Create{Service}Client(endpoint);
```

### 7d. SwitchMode

```csharp
ServiceMode.{Service} => Refresh{Service}ItemsAsync(ct),
```

### 7e. HandleKey — add key binding

```csharp
case ConsoleKey.D5:
    SwitchMode(ServiceMode.{Service}, ct);
    break;
```

Update escape-to-quit guard to include new mode if simple.

### 7f. HandleDelete

```csharp
case ServiceMode.{Service} when _cursor < _{service}Items.Count:
    var s = _{service}Items[_cursor];
    if (Confirm($"Delete {s.Name}?"))
    {
        _statusLine = "Deleting...";
        _ = Delete{Service}ItemAsync(ct);
    }
    break;
```

### 7g. GetRenderable — tabs + help

Tabs line — add `[5]{Name}` to the list, with active highlighting matching existing pattern.

Help text:

```csharp
ServiceMode.{Service} => "↑/↓ nav · del delete · r refresh",
```

### 7h. Render method

```csharp
private void Render{Service}Items(System.Text.StringBuilder content)
{
    content.AppendLine($"[bold white on #0066CC] {Name} [/]");
    if (!string.IsNullOrEmpty(_{service}Error))
        content.AppendLine($"  [yellow]{_{service}Error.EscapeMarkup()}[/]");
    else if (_{service}Items.Count == 0)
        content.AppendLine("  [grey italic](empty)[/]");
    else
        for (int i = 0; i < _{service}Items.Count; i++)
        {
            var prefix = i == _cursor ? "▸" : " ";
            content.AppendLine($"  {prefix} [bold]{_{service}Items[i].Name.EscapeMarkup()}[/]");
        }
}
```

Wire in GetRenderable:

```csharp
case ServiceMode.{Service}:
    Render{Service}Items(content);
    break;
```

### 7i. Refresh method

```csharp
private async Task Refresh{Service}ItemsAsync(CancellationToken ct)
{
    try
    {
        _{service}Items = await {Service}Operations.List{Service}Async(_{service}Client, ct);
        _{service}Error = "";
    }
    catch (Exception ex)
    {
        _{service}Error = ex.Message;
    }
}
```

### 7j. Delete method

```csharp
private async Task Delete{Service}ItemAsync(CancellationToken ct)
{
    try
    {
        var s = _{service}Items[_cursor];
        await {Service}Operations.Delete{Service}Async(_{service}Client, s.{Arn}, ct);
        _statusLine = "Deleted";
        _ = Refresh{Service}ItemsAsync(ct);
    }
    catch (Exception ex)
    {
        _statusLine = $"Error: {ex.Message}";
    }
}
```

### 7k. Dispose

Add `_{service}Client?.Dispose();`.

## 8. Tests

Add factory test in `test/dotstack.Core.Tests/AwsClientFactoryTests.cs`:

```csharp
[Fact]
public void Create{Service}Client_returns_client()
{
    var client = AwsClientFactory.Create{Service}Client("http://localhost:4566");
    client.ShouldNotBeNull();
}
```

---

## Checklist

- [ ] `Directory.Packages.props` — package version added
- [ ] `dotstack.Core.csproj` — package reference added
- [ ] `dotstack.Core/Aws/AwsClientFactory.cs` — config + factory method
- [ ] `dotstack.Core/{Service}/{Service}Operations.cs` — records + static class
- [ ] `dotstack.Cli/Commands/{Service}Commands.cs` — settings + commands
- [ ] `dotstack.Cli/Program.cs` — branch registered
- [ ] `dotstack.Tui/BrowseDashboard.cs` — ServiceMode, fields, constructor, SwitchMode, HandleKey, HandleDelete, GetRenderable, render method, refresh, delete, Dispose
- [ ] `test/dotstack.Core.Tests/AwsClientFactoryTests.cs` — factory test
- [ ] Build passes (`dotnet build`)
- [ ] Tests pass (`dotnet test`)
