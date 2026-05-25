---
name: add-spectre-command
description: Create a new CLI command in dotstack following Spectre.Console.Cli patterns — settings class extending EndpointSettings, command class inheriting Command<TSettings>, registration in Program.cs, and standard output formatting. Use when adding a new CLI subcommand or when user asks to extend the dotstack CLI beyond existing AWS service commands.
---

# Add CLI Command to dotstack

## Quick start

Given `dotstack secret list`:

1. Create `dotstack.Cli/Commands/SecretCommands.cs`
2. Wire into `Program.cs`
3. Build & test

## Workflow

### 1. Create commands file

```csharp
using System.ComponentModel;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public class SecretSettings : EndpointSettings
{
    [CommandOption("-n|--name")]
    [Description("Secret name")]
    public string? Name { get; set; }
}

public class SecretListCommand : Command<SecretSettings>
{
    public override int Execute(CommandContext context, SecretSettings settings)
    {
        // logic here
        AnsiConsole.MarkupLine("[green]done[/]");
        return 0;
    }
}
```

Settings extend `EndpointSettings` to inherit `--endpoint-url` global flag.

### 2. Wire into Program.cs

Add branch or standalone command in `Program.cs`:

```csharp
config.AddBranch("secret", secret =>
{
    secret.AddCommand<SecretListCommand>("list");
});
```

This creates `dotstack secret list`.

For top-level commands (no subcommand), use:
```csharp
config.AddCommand<SecretListCommand>("secret");
```

### 3. Output patterns

| Type | Pattern |
|---|---|
| Success | `AnsiConsole.MarkupLine("[green]done[/]");` |
| Error | `AnsiConsole.MarkupLine("[red]error: {msg}[/]");` |
| Info | `AnsiConsole.MarkupLine("[yellow]{msg}[/]");` |
| Data | `AnsiConsole.WriteLine(data);` |

Return `0` for success, `1` for error.

### 4. Existing command patterns

| File | Pattern |
|---|---|
| `S3Commands.cs` | Multiple subcommands under branch `s3` |
| `InitCommand.cs` | Single standalone command with prompts |
| `ContainerCommands.cs` | Branch `container` with subcommands |
| `BrowseCommand.cs` | Standalone command launching TUI |

### 5. Test

CLI commands are thin wrappers. Test the operation methods they call, not the command handler itself. See `add-unit-tests` skill.
