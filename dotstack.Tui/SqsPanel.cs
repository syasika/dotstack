using Amazon.SQS;
using Spectre.Console;
using DotStack.Core.Aws;
using DotStack.Core.Sqs;

namespace DotStack.Tui;

public class SqsPanel : IServicePanel
{
    private readonly AmazonSQSClient _client;
    private List<Queue> _queues = [];
    private string _queuesError = "";
    private string _currentQueueUrl = "";
    private List<Message> _messages = [];
    private string _messagesError = "";
    private bool _showingMessages;
    private int _cursor;
    private string _statusLine = "";

    public SqsPanel(AmazonSQSClient client)
    {
        _client = client;
    }

    public string HelpText => _showingMessages
        ? "↑/↓ nav · del delete · esc back · r refresh"
        : "↑/↓ nav · enter messages · del delete · r refresh";

    public bool IsAtRootLevel => !_showingMessages;

    public bool HandleKey(ConsoleKeyInfo key, CancellationToken ct)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.K:
                if (_cursor > 0) _cursor--;
                return true;

            case ConsoleKey.DownArrow or ConsoleKey.J:
                _cursor++;
                return true;

            case ConsoleKey.Enter:
                HandleEnter(ct);
                return true;

            case ConsoleKey.R:
                HandleRefresh(ct);
                return true;

            case ConsoleKey.Delete or ConsoleKey.Backspace:
                HandleDelete(ct);
                return true;

            case ConsoleKey.Escape:
                if (_showingMessages)
                {
                    _showingMessages = false;
                    _currentQueueUrl = "";
                    _cursor = 0;
                    _statusLine = "";
                    return true;
                }
                return false;
        }

        return false;
    }

    public Task RefreshAsync(CancellationToken ct)
    {
        if (_showingMessages)
            return RefreshMessagesAsync(ct);
        return RefreshQueuesAsync(ct);
    }

    private void HandleEnter(CancellationToken ct)
    {
        if (!_showingMessages && _cursor < _queues.Count)
        {
            _currentQueueUrl = _queues[_cursor].Url;
            _showingMessages = true;
            _cursor = 0;
            _statusLine = "Receiving messages...";
            _ = RefreshMessagesAsync(ct);
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
        if (!_showingMessages && _cursor < _queues.Count)
        {
            var q = _queues[_cursor];
            if (Confirm($"Delete queue '{q.Name}'?"))
            {
                _statusLine = "Deleting...";
                _ = DeleteQueueAsync(ct);
            }
        }
        else if (_showingMessages && _cursor < _messages.Count)
        {
            var m = _messages[_cursor];
            if (Confirm($"Delete message {m.Id}?"))
            {
                _statusLine = "Deleting...";
                _ = DeleteMessageAsync(ct);
            }
        }
    }

    public void Render(System.Text.StringBuilder content)
    {
        if (_showingMessages)
            RenderMessages(content);
        else
            RenderQueues(content);

        if (!string.IsNullOrEmpty(_statusLine))
            content.AppendLine($"  [italic #00AAAA]{_statusLine.EscapeMarkup()}[/]");
    }

    private void RenderQueues(System.Text.StringBuilder content)
    {
        content.AppendLine($"[bold white on #0066CC] SQS Queues [/]");
        if (!string.IsNullOrEmpty(_queuesError))
            content.AppendLine($"  [yellow]{_queuesError.EscapeMarkup()}[/]");
        else if (_queues.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _queues.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                content.AppendLine($"  {prefix} 📦 {_queues[i].Name.EscapeMarkup()}");
            }
    }

    private void RenderMessages(System.Text.StringBuilder content)
    {
        var qName = SqsOperations.ExtractQueueName(_currentQueueUrl);
        content.AppendLine($"[bold white on #0066CC] Messages — {qName.EscapeMarkup()} [/]");
        if (!string.IsNullOrEmpty(_messagesError))
            content.AppendLine($"  [yellow]{_messagesError.EscapeMarkup()}[/]");
        else if (_messages.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _messages.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                var body = _messages[i].Body.Length > 80
                    ? _messages[i].Body[..80] + "..."
                    : _messages[i].Body;
                content.AppendLine($"  {prefix} {body.EscapeMarkup()}  [grey]({_messages[i].Id})[/]");
            }
    }

    private async Task RefreshQueuesAsync(CancellationToken ct)
    {
        try
        {
            _queues = await SqsOperations.ListQueuesAsync(_client, ct);
            _queuesError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _queuesError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task RefreshMessagesAsync(CancellationToken ct)
    {
        try
        {
            _messages = await SqsOperations.ReceiveMessagesAsync(_client, _currentQueueUrl, 10, ct);
            _messagesError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _messagesError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task DeleteQueueAsync(CancellationToken ct)
    {
        try
        {
            var q = _queues[_cursor];
            await SqsOperations.DeleteQueueAsync(_client, q.Url, ct);
            _statusLine = "Deleted";
            _ = RefreshAsync(ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteMessageAsync(CancellationToken ct)
    {
        try
        {
            var m = _messages[_cursor];
            await SqsOperations.DeleteMessageAsync(_client, _currentQueueUrl, m.ReceiptHandle, ct);
            _statusLine = "Deleted";
            _ = RefreshMessagesAsync(ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private static bool Confirm(string prompt) =>
        AnsiConsole.Confirm(prompt, false);

    public void Dispose()
    {
        _client?.Dispose();
    }
}
