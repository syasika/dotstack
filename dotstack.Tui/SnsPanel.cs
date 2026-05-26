using Amazon.SimpleNotificationService;
using DotStack.Core.Aws;
using DotStack.Core.Sns;
using Spectre.Console;

namespace DotStack.Tui;

public class SnsPanel : IServicePanel
{
    private readonly IAmazonSimpleNotificationService _client;
    private List<Topic> _topics = [];
    private string _topicsError = "";
    private int _cursor;
    private string _statusLine = "";

    public SnsPanel(IAmazonSimpleNotificationService client)
    {
        _client = client;
    }

    public string HelpText => "↑/↓ nav · del delete · r refresh";

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

    public Task RefreshAsync(CancellationToken ct) => RefreshTopicsAsync(ct);

    private void HandleRefresh(CancellationToken ct)
    {
        _cursor = 0;
        _statusLine = "Refreshing...";
        _ = RefreshAsync(ct);
    }

    private void HandleDelete(CancellationToken ct)
    {
        if (_cursor < _topics.Count)
        {
            var t = _topics[_cursor];
            if (Confirm($"Delete topic '{t.Name}'?"))
            {
                _statusLine = "Deleting...";
                _ = DeleteTopicAsync(ct);
            }
        }
    }

    public void Render(System.Text.StringBuilder content)
    {
        content.AppendLine($"[bold white on #0066CC] SNS Topics [/]");

        if (!string.IsNullOrEmpty(_topicsError))
            content.AppendLine($"  [yellow]{_topicsError.EscapeMarkup()}[/]");
        else if (_topics.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _topics.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                content.AppendLine($"  {prefix} 📢 {_topics[i].Name.EscapeMarkup()}");
            }

        if (!string.IsNullOrEmpty(_statusLine))
            content.AppendLine($"  [italic #00AAAA]{_statusLine.EscapeMarkup()}[/]");
    }

    private async Task RefreshTopicsAsync(CancellationToken ct)
    {
        try
        {
            _topics = await SnsOperations.ListTopicsAsync(_client, ct);
            _topicsError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _topicsError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task DeleteTopicAsync(CancellationToken ct)
    {
        try
        {
            var t = _topics[_cursor];
            await SnsOperations.DeleteTopicAsync(_client, t.Arn, ct);
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
