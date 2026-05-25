using Spectre.Console;
using DotStack.Core.Aws;
using DotStack.Core.Configuration;
using DotStack.Core.S3;
using DotStack.Core.Ssm;
using DotStack.Core.Sqs;
using DotStack.Core.Sns;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Amazon.SimpleNotificationService;
using Docker.DotNet;

namespace DotStack.Tui;

public enum ServiceMode { S3, Ssm, Sqs, Sns }

public class BrowseDashboard : IDisposable
{
    private readonly string _endpoint;
    private readonly AmazonS3Client _s3Client;
    private readonly AmazonSimpleSystemsManagementClient _ssmClient;
    private readonly AmazonSQSClient _sqsClient;
    private readonly AmazonSimpleNotificationServiceClient _snsClient;
    private readonly DockerClient? _dockerClient;
    private readonly CancellationTokenSource _cts = new();

    // Container state
    private string _containerStatus = "checking...";
    private string _containerName = "";

    // Navigation
    private ServiceMode _mode = ServiceMode.S3;
    private int _cursor;

    // S3 state
    private List<string> _buckets = [];
    private string _bucketsError = "";
    private string _currentBucket = "";
    private List<S3Object> _objects = [];
    private string _objectsError = "";
    private bool _showingObjects;

    // SSM state
    private List<SsmParameter> _parameters = [];
    private string _parametersError = "";
    private string? _ssmNextToken;
    private readonly List<string> _ssmPrevTokens = [];
    private string? _ssmCurrentToken;
    private string _ssmPageLabel = "";

    // SQS state
    private List<Queue> _queues = [];
    private string _queuesError = "";
    private string _currentQueueUrl = "";
    private List<Message> _messages = [];
    private string _messagesError = "";
    private bool _showingMessages;

    // SNS state
    private List<Topic> _topics = [];
    private string _topicsError = "";

    // UI state
    private string _statusLine = "";
    private bool _running = true;

    public BrowseDashboard(string endpoint)
    {
        _endpoint = endpoint;

        var cfg = AwsClientFactory.CreateS3Client(endpoint);
        _s3Client = (AmazonS3Client)cfg;
        _ssmClient = (AmazonSimpleSystemsManagementClient)(object)AwsClientFactory.CreateSsmClient(endpoint);
        _sqsClient = (AmazonSQSClient)(object)AwsClientFactory.CreateSqsClient(endpoint);
        _snsClient = (AmazonSimpleNotificationServiceClient)(object)AwsClientFactory.CreateSnsClient(endpoint);

        try
        {
            _dockerClient = new DockerClientConfiguration().CreateClient();
        }
        catch
        {
            _dockerClient = null;
        }
    }

    public void Run()
    {
        var cts = _cts;
        _ = RefreshContainerStatusAsync(cts.Token);
        _ = RefreshS3BucketsAsync(cts.Token);

        AnsiConsole.Live(GetRenderable())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(ctx =>
            {
                while (_running && !cts.Token.IsCancellationRequested)
                {
                    // Handle input
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleKey(key, cts.Token);
                    }

                    // Update display
                    ctx.UpdateTarget(GetRenderable());
                    ctx.Refresh();
                    Thread.Sleep(50);
                }
            });
    }

    private void HandleKey(ConsoleKeyInfo key, CancellationToken ct)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape when _mode == ServiceMode.S3 && !_showingObjects:
                _running = false;
                return;

            case ConsoleKey.D1:
                SwitchMode(ServiceMode.S3, ct);
                break;
            case ConsoleKey.D2:
                SwitchMode(ServiceMode.Ssm, ct);
                break;
            case ConsoleKey.D3:
                SwitchMode(ServiceMode.Sqs, ct);
                break;
            case ConsoleKey.D4:
                SwitchMode(ServiceMode.Sns, ct);
                break;

            case ConsoleKey.UpArrow or ConsoleKey.K:
                if (_cursor > 0) _cursor--;
                break;
            case ConsoleKey.DownArrow or ConsoleKey.J:
                _cursor++;
                break;

            case ConsoleKey.Enter:
                HandleEnter(ct);
                break;

            case ConsoleKey.R:
                HandleRefresh(ct);
                break;

            case ConsoleKey.Delete or ConsoleKey.Backspace:
                HandleDelete(ct);
                break;
        }
    }

    private void SwitchMode(ServiceMode mode, CancellationToken ct)
    {
        _mode = mode;
        _cursor = 0;
        _showingObjects = false;
        _showingMessages = false;
        _statusLine = "";

        _ = mode switch
        {
            ServiceMode.S3 => RefreshS3BucketsAsync(ct),
            ServiceMode.Ssm => RefreshSsmParametersAsync(null, ct),
            ServiceMode.Sqs => RefreshSqsQueuesAsync(ct),
            ServiceMode.Sns => RefreshSnsTopicsAsync(ct),
            _ => Task.CompletedTask
        };
    }

    private void HandleEnter(CancellationToken ct)
    {
        switch (_mode)
        {
            case ServiceMode.S3 when !_showingObjects && _cursor < _buckets.Count:
                _currentBucket = _buckets[_cursor];
                _showingObjects = true;
                _cursor = 0;
                _statusLine = "Loading objects...";
                _ = RefreshS3ObjectsAsync(ct);
                break;

            case ServiceMode.Ssm when _cursor < _parameters.Count:
                var p = _parameters[_cursor];
                _statusLine = $"Getting value for {p.Name}...";
                _ = GetParameterValueAsync(p.Name, ct);
                break;

            case ServiceMode.Sqs when !_showingMessages && _cursor < _queues.Count:
                _currentQueueUrl = _queues[_cursor].Url;
                _showingMessages = true;
                _cursor = 0;
                _statusLine = "Receiving messages...";
                _ = RefreshSqsMessagesAsync(ct);
                break;
        }
    }

    private void HandleRefresh(CancellationToken ct)
    {
        _cursor = 0;
        _statusLine = "Refreshing...";
        _ = _mode switch
        {
            ServiceMode.S3 => RefreshS3BucketsAsync(ct),
            ServiceMode.Ssm => RefreshSsmParametersAsync(null, ct),
            ServiceMode.Sqs when _showingMessages => RefreshSqsMessagesAsync(ct),
            ServiceMode.Sqs => RefreshSqsQueuesAsync(ct),
            ServiceMode.Sns => RefreshSnsTopicsAsync(ct),
            _ => Task.CompletedTask
        };
    }

    private void HandleDelete(CancellationToken ct)
    {
        switch (_mode)
        {
            case ServiceMode.S3 when _showingObjects && _cursor < _objects.Count:
                var obj = _objects[_cursor];
                if (obj.Key.EndsWith('/')) return;
                if (Confirm($"Delete s3://{_currentBucket}/{obj.Key}?"))
                {
                    _statusLine = "Deleting...";
                    _ = DeleteS3ObjectAsync(ct);
                }
                break;

            case ServiceMode.Ssm when _cursor < _parameters.Count:
                var p = _parameters[_cursor];
                if (Confirm($"Delete parameter '{p.Name}'?"))
                {
                    _statusLine = "Deleting...";
                    _ = DeleteParameterAsync(ct);
                }
                break;

            case ServiceMode.Sqs when !_showingMessages && _cursor < _queues.Count:
                var q = _queues[_cursor];
                if (Confirm($"Delete queue '{q.Name}'?"))
                {
                    _statusLine = "Deleting...";
                    _ = DeleteQueueAsync(ct);
                }
                break;

            case ServiceMode.Sqs when _showingMessages && _cursor < _messages.Count:
                var m = _messages[_cursor];
                if (Confirm($"Delete message {m.Id}?"))
                {
                    _statusLine = "Deleting...";
                    _ = DeleteMessageAsync(ct);
                }
                break;

            case ServiceMode.Sns when _cursor < _topics.Count:
                var t = _topics[_cursor];
                if (Confirm($"Delete topic '{t.Name}'?"))
                {
                    _statusLine = "Deleting...";
                    _ = DeleteTopicAsync(ct);
                }
                break;
        }
    }

    private static bool Confirm(string prompt) =>
        AnsiConsole.Confirm(prompt, false);

    private Panel GetRenderable()
    {
        var content = new System.Text.StringBuilder();

        // Header
        content.AppendLine($"[bold white on #3E22B2]☁  dotstack — ministack dashboard[/]");
        content.AppendLine();

        // Container status
        var statusColor = _containerStatus.Contains("running", StringComparison.OrdinalIgnoreCase)
            ? "green" : "yellow";
        content.AppendLine($"[bold white on #0066CC] Container [/]");
        content.AppendLine($"  [{statusColor} bold]● {_containerStatus.EscapeMarkup()}[/]  ({_containerName.EscapeMarkup()})");
        content.AppendLine();

        // Service tabs
        var tabs = _mode switch
        {
            ServiceMode.S3 => "[bold] [1][white]S3[/]  [2]SSM[/]  [3]SQS[/]  [4]SNS[/] [/]",
            ServiceMode.Ssm => "[bold] [1]S3[/]  [2][white]SSM[/]  [3]SQS[/]  [4]SNS[/] [/]",
            ServiceMode.Sqs => "[bold] [1]S3[/]  [2]SSM[/]  [3][white]SQS[/]  [4]SNS[/] [/]",
            ServiceMode.Sns => "[bold] [1]S3[/]  [2]SSM[/]  [3]SQS[/]  [4][white]SNS[/] [/]",
            _ => "[bold] [1]S3[/]  [2]SSM[/]  [3]SQS[/]  [4]SNS[/] [/]"
        };
        content.AppendLine($"  {tabs}");
        content.AppendLine();

        // Service content
        switch (_mode)
        {
            case ServiceMode.S3:
                if (_showingObjects)
                    RenderObjects(content);
                else
                    RenderBuckets(content);
                break;
            case ServiceMode.Ssm:
                RenderSsmParameters(content);
                break;
            case ServiceMode.Sqs:
                if (_showingMessages)
                    RenderMessages(content);
                else
                    RenderQueues(content);
                break;
            case ServiceMode.Sns:
                RenderTopics(content);
                break;
        }
        content.AppendLine();

        // Status line
        if (!string.IsNullOrEmpty(_statusLine))
            content.AppendLine($"  [italic #00AAAA]{_statusLine.EscapeMarkup()}[/]");

        // Help bar
        var help = _mode switch
        {
            ServiceMode.S3 when _showingObjects => "↑/↓ nav · del delete · esc back · r refresh",
            ServiceMode.S3 => "↑/↓ nav · enter browse · r refresh",
            ServiceMode.Ssm => "↑/↓ nav · enter value · del delete · [ ] page · r refresh",
            ServiceMode.Sqs when _showingMessages => "↑/↓ nav · del delete · esc back · r refresh",
            ServiceMode.Sqs => "↑/↓ nav · enter messages · del delete · r refresh",
            ServiceMode.Sns => "↑/↓ nav · del delete · r refresh",
            _ => ""
        };
        help += _containerStatus.Contains("running") ? " · x stop" : " · s start";
        help += " · q quit";
        content.AppendLine($"[grey]{help}[/]");

        return new Panel(new Markup(content.ToString()))
        {
            Padding = new Padding(0, 0, 0, 0),
            Border = BoxBorder.None
        };
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
        content.AppendLine($"[bold white on #0066CC] Objects — {_currentBucket.EscapeMarkup()} [/]");
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
                var size = _objects[i].Size > 0 && !_objects[i].Key.EndsWith('/')
                    ? $" [grey]({_objects[i].Size} bytes)[/]"
                    : "";
                content.AppendLine($"  {prefix} {icon} [{color}]{_objects[i].Key.EscapeMarkup()}[/]{size}");
            }
    }

    private void RenderSsmParameters(System.Text.StringBuilder content)
    {
        var label = "SSM Parameters";
        if (!string.IsNullOrEmpty(_ssmPageLabel))
            label += $"  {_ssmPageLabel}";
        content.AppendLine($"[bold white on #0066CC] {label} [/]");

        if (!string.IsNullOrEmpty(_parametersError))
            content.AppendLine($"  [yellow]{_parametersError.EscapeMarkup()}[/]");
        else if (_parameters.Count == 0)
            content.AppendLine("  [grey italic](empty)[/]");
        else
            for (int i = 0; i < _parameters.Count; i++)
            {
                var prefix = i == _cursor ? "▸" : " ";
                var p = _parameters[i];
                content.AppendLine($"  {prefix} [bold]{p.Name.EscapeMarkup()}[/]  [grey]({p.Type}, v{p.Version})[/]");
            }
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

    private void RenderTopics(System.Text.StringBuilder content)
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
    }

    // --- Background refresh operations ---

    private async Task RefreshContainerStatusAsync(CancellationToken ct)
    {
        try
        {
            var config = Config.Load();
            if (config is null || _dockerClient is null)
            {
                _containerStatus = "not initialized";
                return;
            }

            _containerName = config.ContainerName;
            var response = await _dockerClient.Containers.InspectContainerAsync(
                config.ContainerName, ct);
            _containerStatus = response.State.Status;
        }
        catch
        {
            _containerStatus = "stopped";
        }
    }

    private async Task RefreshS3BucketsAsync(CancellationToken ct)
    {
        try
        {
            _buckets = await S3Operations.ListBucketsAsync(_s3Client, ct);
            _bucketsError = "";
        }
        catch (Exception ex)
        {
            _bucketsError = ex.Message;
        }
    }

    private async Task RefreshS3ObjectsAsync(CancellationToken ct)
    {
        try
        {
            _objects = await S3Operations.ListObjectsAsync(_s3Client, _currentBucket, "", ct);
            _objectsError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _objectsError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task RefreshSsmParametersAsync(string? token, CancellationToken ct)
    {
        try
        {
            var requestToken = token ?? _ssmCurrentToken;
            var page = await SsmOperations.ListParametersAsync(_ssmClient, requestToken, 20, ct);
            _parameters = [.. page.Parameters];
            _ssmCurrentToken = requestToken;
            _ssmNextToken = page.NextToken;
            _ssmPageLabel = page.NextToken is not null ? "[more →]" : "";
            _parametersError = "";
        }
        catch (Exception ex)
        {
            _parametersError = ex.Message;
        }
    }

    private async Task RefreshSqsQueuesAsync(CancellationToken ct)
    {
        try
        {
            _queues = await SqsOperations.ListQueuesAsync(_sqsClient, ct);
            _queuesError = "";
        }
        catch (Exception ex)
        {
            _queuesError = ex.Message;
        }
    }

    private async Task RefreshSqsMessagesAsync(CancellationToken ct)
    {
        try
        {
            _messages = await SqsOperations.ReceiveMessagesAsync(_sqsClient, _currentQueueUrl, 10, ct);
            _messagesError = "";
            _statusLine = "";
        }
        catch (Exception ex)
        {
            _messagesError = ex.Message;
            _statusLine = "";
        }
    }

    private async Task RefreshSnsTopicsAsync(CancellationToken ct)
    {
        try
        {
            _topics = await SnsOperations.ListTopicsAsync(_snsClient, ct);
            _topicsError = "";
        }
        catch (Exception ex)
        {
            _topicsError = ex.Message;
        }
    }

    private async Task GetParameterValueAsync(string name, CancellationToken ct)
    {
        try
        {
            var p = await SsmOperations.GetParameterAsync(_ssmClient, name, ct);
            _statusLine = $"{p.Name}: {p.Value.EscapeMarkup()}";
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteS3ObjectAsync(CancellationToken ct)
    {
        try
        {
            var obj = _objects[_cursor];
            await S3Operations.DeleteObjectAsync(_s3Client, _currentBucket, obj.Key, ct);
            _statusLine = "Deleted";
            _ = RefreshS3ObjectsAsync(ct);
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
            await SsmOperations.DeleteParameterAsync(_ssmClient, p.Name, ct);
            _statusLine = "Deleted";
            _ = RefreshSsmParametersAsync(null, ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteQueueAsync(CancellationToken ct)
    {
        try
        {
            var q = _queues[_cursor];
            await SqsOperations.DeleteQueueAsync(_sqsClient, q.Url, ct);
            _statusLine = "Deleted";
            _ = RefreshSqsQueuesAsync(ct);
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
            await SqsOperations.DeleteMessageAsync(_sqsClient, _currentQueueUrl, m.ReceiptHandle, ct);
            _statusLine = "Deleted";
            _ = RefreshSqsMessagesAsync(ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteTopicAsync(CancellationToken ct)
    {
        try
        {
            var t = _topics[_cursor];
            await SnsOperations.DeleteTopicAsync(_snsClient, t.Arn, ct);
            _statusLine = "Deleted";
            _ = RefreshSnsTopicsAsync(ct);
        }
        catch (Exception ex)
        {
            _statusLine = $"Error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _s3Client?.Dispose();
        _ssmClient?.Dispose();
        _sqsClient?.Dispose();
        _snsClient?.Dispose();
        _dockerClient?.Dispose();
    }
}
