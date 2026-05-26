using Amazon.S3;
using DotStack.Core.Aws;
using DotStack.Core.S3;
using Spectre.Console;

namespace DotStack.Tui;

public class S3Panel : IServicePanel
{
    private readonly IAmazonS3 _client;
    private List<string> _buckets = [];
    private string _bucketsError = "";
    private string _currentBucket = "";
    private List<S3Object> _objects = [];
    private string _objectsError = "";
    private bool _showingObjects;
    private int _cursor;
    private string _statusLine = "";

    public S3Panel(IAmazonS3 client)
    {
        _client = client;
    }

    public string HelpText =>
        _showingObjects
            ? "↑/↓ nav · del delete · esc back · r refresh"
            : "↑/↓ nav · enter browse · r refresh";

    public bool IsAtRootLevel => !_showingObjects;

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

            case ConsoleKey.Escape:
                if (_showingObjects)
                {
                    _showingObjects = false;
                    _currentBucket = "";
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
        if (_showingObjects)
            return RefreshObjectsAsync(ct);
        return RefreshBucketsAsync(ct);
    }

    private void HandleEnter(CancellationToken ct)
    {
        if (!_showingObjects && _cursor < _buckets.Count)
        {
            _currentBucket = _buckets[_cursor];
            _showingObjects = true;
            _cursor = 0;
            _statusLine = "Loading objects...";
            _ = RefreshObjectsAsync(ct);
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
        if (_showingObjects && _cursor < _objects.Count)
        {
            var obj = _objects[_cursor];
            if (obj.Key.EndsWith('/'))
                return;
            if (Confirm($"Delete s3://{_currentBucket}/{obj.Key}?"))
            {
                _statusLine = "Deleting...";
                _ = DeleteObjectAsync(ct);
            }
        }
    }

    public void Render(System.Text.StringBuilder content)
    {
        if (_showingObjects)
            RenderObjects(content);
        else
            RenderBuckets(content);

        if (!string.IsNullOrEmpty(_statusLine))
            content.AppendLine($"  [italic #00AAAA]{_statusLine.EscapeMarkup()}[/]");
    }

    private void RenderBuckets(System.Text.StringBuilder content)
    {
        content.AppendLine($"[bold white on #0066CC] S3 Buckets [/]");
        if (!string.IsNullOrEmpty(_bucketsError))
            content.AppendLine($"  [yellow]{_bucketsError.EscapeMarkup()}[/]");
        else if (_buckets.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _buckets.Count; i++)
                content.AppendLine($"  {(i == _cursor ? "▸" : " ")} {_buckets[i].EscapeMarkup()}");
    }

    private void RenderObjects(System.Text.StringBuilder content)
    {
        content.AppendLine(
            $"[bold white on #0066CC] Objects — {_currentBucket.EscapeMarkup()} [/]"
        );
        if (!string.IsNullOrEmpty(_objectsError))
            content.AppendLine($"  [yellow]{_objectsError.EscapeMarkup()}[/]");
        else if (_objects.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _objects.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                var icon = _objects[i].Key.EndsWith('/') ? "📁" : "📄";
                var color = _objects[i].Key.EndsWith('/') ? "#00AAAA" : "white";
                var size =
                    _objects[i].Size > 0 && !_objects[i].Key.EndsWith('/')
                        ? $" [grey]({_objects[i].Size} bytes)[/]"
                        : "";
                content.AppendLine(
                    $"  {prefix} {icon} [{color}]{_objects[i].Key.EscapeMarkup()}[/]{size}"
                );
            }
    }

    private async Task RefreshBucketsAsync(CancellationToken ct)
    {
        try
        {
            _buckets = await S3Operations.ListBucketsAsync(_client, ct);
            _bucketsError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _bucketsError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task RefreshObjectsAsync(CancellationToken ct)
    {
        try
        {
            _objects = await S3Operations.ListObjectsAsync(_client, _currentBucket, "", ct);
            _objectsError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _objectsError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task DeleteObjectAsync(CancellationToken ct)
    {
        try
        {
            var obj = _objects[_cursor];
            await S3Operations.DeleteObjectAsync(_client, _currentBucket, obj.Key, ct);
            _statusLine = "Deleted";
            _ = RefreshObjectsAsync(ct);
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
