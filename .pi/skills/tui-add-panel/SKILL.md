---
name: tui-add-panel
description: Add a new service panel to dotstack TUI dashboard (BrowseDashboard.cs) — ServiceMode enum, state fields, client, refresh/delete handlers, render method, key handling, help text, Dispose. Use when adding a new service panel or extending the browse TUI.
---

# Add TUI Panel to BrowseDashboard

## Quick start

```csharp
// 1. Add enum value
public enum ServiceMode { S3, Ssm, Sqs, Sns, DynamoDb }

// 2. Add state fields + client
private readonly IAmazonDynamoDB _ddbClient;
private List<Table> _tables = [];
private string _tablesError = "";

// 3. Init client in constructor
_ddbClient = (AmazonDynamoDBClient)(object)AwsClientFactory.CreateDynamoDbClient(endpoint);
```

## Wire into 4 dispatch methods

| Method | Add line |
|---|---|
| `SwitchMode` | `ServiceMode.DynamoDb => RefreshDdbTablesAsync(ct)` |
| `HandleKey` | `case ConsoleKey.D5: SwitchMode(ServiceMode.DynamoDb, ct); break;` |
| `HandleEnter` | `case ServiceMode.DynamoDb when _cursor < _tables.Count: ... break;` |
| `HandleDelete` | `case ServiceMode.DynamoDb when _cursor < _tables.Count: ... break;` |
| `HandleRefresh` | `ServiceMode.DynamoDb => RefreshDdbTablesAsync(ct)` |

## Add render method

```csharp
private void RenderDdbTables(StringBuilder content)
{
    content.AppendLine($"[bold white on #0066CC] DynamoDB Tables [/]");
    if (!string.IsNullOrEmpty(_tablesError))
        content.AppendLine($"  [yellow]{_tablesError.EscapeMarkup()}[/]");
    else if (_tables.Count == 0)
        content.AppendLine("  [grey italic](empty)[/]");
    else
        for (int i = 0; i < _tables.Count; i++)
            content.AppendLine($"  {(i == _cursor ? "▸" : " ")} {_tables[i].Name.EscapeMarkup()}");
}
```

Call from `GetRenderable()` → `case ServiceMode.DynamoDb:`.

## Update getRenderable

- Tab line: `[1]S3  [2]SSM  [3]SQS  [4]SNS  [5][white]DDB[/]`
- Help text for new mode
- Add to `Dispose`

## Background refresh pattern

```csharp
private async Task RefreshDdbTablesAsync(CancellationToken ct)
{
    try
    {
        _tables = await DynamoDbOperations.ListTablesAsync(_ddbClient, ct);
        _tablesError = "";
    }
    catch (Exception ex)
    {
        _tablesError = ex.Message;
    }
}
```

See existing `S3Operations`, `SsmOperations` in `dotstack.Core/`.
