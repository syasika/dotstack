---
name: add-config-field
description: Add a new field to the dotstack Config record (dotstack.Core/Configuration/Config.cs) with proper JSON serialization (camelCase), Save/Load compatibility, and backward compatibility for existing config files. Use when extending configuration or when user asks to add a new config setting.
---

# Add Config Field to dotstack

## Quick start

Add field to `Config` record in `dotstack.Core/Configuration/Config.cs`:

1. Add property to record
2. Update usages (Commands, Tui)
3. Handle missing values (backward compat)

## Workflow

### 1. Add property

Config is a positional record. Add parameter:
```csharp
public record Config(
    string ContainerName,
    string ImageName,
    string Port,
    string EndpointUrl,
    string? NewField = null  // nullable + default = backward compat
)
```

Use nullable + default value for new fields so existing `config.json` files without the field still deserialize.

### 2. JSON naming

`JsonSerializerOptions` uses `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`. New field `NewField` serializes as `"newField"` in JSON. No extra attributes needed.

### 3. Update callers

Search all files that construct `new Config(...)` or reference `.EndpointUrl` etc. Update to pass new field or accept that existing usage may get `null`.

Key callers:
- `dotstack.Cli/Commands/InitCommand.cs` — creates Config interactively
- `dotstack.Tui/BrowseDashboard.cs` — reads config fields
- `dotstack.Cli/Commands/ContainerCommands.cs` — uses config
- Any command that loads config

### 4. Backward compatibility check

When a field is nullable with default `null`, existing `~/.dotstack/config.json` without the field loads fine — `JsonSerializer.Deserialize<Config>` leaves it null. Code reading the field should handle null.

### 5. Existing config shape

Current `config.json`:
```json
{
  "containerName": "ministack",
  "imageName": "ministackorg/ministack",
  "port": "4566",
  "endpointUrl": "http://localhost:4566"
}
```

New fields are additive — don't change existing field names or types.

### 6. Test

```csharp
[Fact]
public void NewField_defaults_to_null()
{
    var cfg = new Config("c", "i", "1", "u");
    cfg.NewField.ShouldBeNull();
}
```
