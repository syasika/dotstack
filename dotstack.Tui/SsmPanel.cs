using Amazon.SimpleSystemsManagement;
using DotStack.Core.Aws;
using DotStack.Core.Ssm;
using Spectre.Console;

namespace DotStack.Tui;

public class SsmPanel : IServicePanel
{
    private readonly AmazonSimpleSystemsManagementClient _client;
    private List<SsmParameter> _parameters = [];
    private string _parametersError = "";
    private int _cursor;
    private string _statusLine = "";

    public SsmPanel(AmazonSimpleSystemsManagementClient client)
    {
        _client = client;
    }

    public string HelpText => "↑/↓ nav · enter value · del delete · r refresh";

    public bool IsAtRootLevel => true;

    public bool HandleKey(ConsoleKeyInfo key, CancellationToken ct)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.K:
                if (_cursor > 0)
                    _cursor--;
                return true;

            case ConsoleKey.DownArrow
            or ConsoleKey.J:
                _cursor++;
                return true;

            case ConsoleKey.Enter:
                HandleEnter(ct);
                return true;

            case ConsoleKey.R:
                HandleRefresh(ct);
                return true;

            case ConsoleKey.Delete
            or ConsoleKey.Backspace:
                HandleDelete(ct);
                return true;
        }

        return false;
    }

    public Task RefreshAsync(CancellationToken ct) => RefreshParametersAsync(ct);

    private void HandleEnter(CancellationToken ct)
    {
        if (_cursor < _parameters.Count)
        {
            var p = _parameters[_cursor];
            _statusLine = $"Getting value for {p.Name}...";
            _ = GetParameterValueAsync(p.Name, ct);
        }
    }

    private void HandleRefresh(CancellationToken ct)
    {
        _cursor = 0;
        _statusLine = "Refreshing...";
        _ = RefreshAsync(ct);
    }

    private void HandleDelete(CancellationToken ct)
    {
        if (_cursor < _parameters.Count)
        {
            var p = _parameters[_cursor];
            if (Confirm($"Delete parameter '{p.Name}'?"))
            {
                _statusLine = "Deleting...";
                _ = DeleteParameterAsync(ct);
            }
        }
    }

    public void Render(System.Text.StringBuilder content)
    {
        content.AppendLine($"[bold white on #0066CC] SSM Parameters [/]");

        if (!string.IsNullOrEmpty(_parametersError))
            content.AppendLine($"  [yellow]{_parametersError.EscapeMarkup()}[/]");
        else if (_parameters.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _parameters.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                var p = _parameters[i];
                content.AppendLine(
                    $"  {prefix} [bold]{p.Name.EscapeMarkup()}[/]  [grey]({p.Type}, v{p.Version})[/]"
                );
            }

        if (!string.IsNullOrEmpty(_statusLine))
            content.AppendLine($"  [italic #00AAAA]{_statusLine.EscapeMarkup()}[/]");
    }

    private async Task RefreshParametersAsync(CancellationToken ct)
    {
        try
        {
            _parameters = await SsmOperations.ListAllParametersAsync(_client, ct);
            _parametersError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _parametersError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task GetParameterValueAsync(string name, CancellationToken ct)
    {
        try
        {
            var p = await SsmOperations.GetParameterAsync(_client, name, ct);
            _statusLine = $"{p.Name}: {p.Value.EscapeMarkup()}";
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteParameterAsync(CancellationToken ct)
    {
        try
        {
            var p = _parameters[_cursor];
            await SsmOperations.DeleteParameterAsync(_client, p.Name, ct);
            _statusLine = "Deleted";
            _ = RefreshAsync(ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private static bool Confirm(string prompt) => AnsiConsole.Confirm(prompt, false);

    public void Dispose()
    {
        _client?.Dispose();
    }
}
