using Spectre.Console;
using DotStack.Core.Aws;
using DotStack.Core.Configuration;
using DotStack.Core.S3;
using Docker.DotNet;

namespace DotStack.Tui;

public enum ServiceMode { S3, Ssm, Sqs, Sns }

public class BrowseDashboard : IDisposable
{
    private readonly Dictionary<ServiceMode, IServicePanel> _panels;
    private readonly DockerClient? _dockerClient;
    private readonly CancellationTokenSource _cts = new();

    // Container state
    private string _containerStatus = "checking...";
    private string _containerName = "";

    // Navigation
    private ServiceMode _mode = ServiceMode.S3;
    private bool _running = true;

    public BrowseDashboard(string endpoint)
    {
        _panels = new Dictionary<ServiceMode, IServicePanel>
        {
            [ServiceMode.S3] = new S3Panel(AwsClientFactory.CreateS3Client(endpoint)),
            [ServiceMode.Ssm] = new SsmPanel(AwsClientFactory.CreateSsmClient(endpoint)),
            [ServiceMode.Sqs] = new SqsPanel(AwsClientFactory.CreateSqsClient(endpoint)),
            [ServiceMode.Sns] = new SnsPanel(AwsClientFactory.CreateSnsClient(endpoint)),
        };

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
        _ = _panels[_mode].RefreshAsync(cts.Token);

        AnsiConsole.Live(GetRenderable())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(ctx =>
            {
                while (_running && !cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleKey(key, cts.Token);
                    }

                    ctx.UpdateTarget(GetRenderable());
                    ctx.Refresh();
                    Thread.Sleep(50);
                }
            });
    }

    private void HandleKey(ConsoleKeyInfo key, CancellationToken ct)
    {
        // Mode switching (always handled)
        switch (key.Key)
        {
            case ConsoleKey.D1: SwitchMode(ServiceMode.S3, ct); return;
            case ConsoleKey.D2: SwitchMode(ServiceMode.Ssm, ct); return;
            case ConsoleKey.D3: SwitchMode(ServiceMode.Sqs, ct); return;
            case ConsoleKey.D4: SwitchMode(ServiceMode.Sns, ct); return;
        }

        // Delegate to active panel
        if (_panels[_mode].HandleKey(key, ct))
            return;

        // Global fallback keys
        switch (key.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                _running = false;
                return;
        }
    }

    private void SwitchMode(ServiceMode mode, CancellationToken ct)
    {
        _mode = mode;
        _ = _panels[mode].RefreshAsync(ct);
    }

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
            ServiceMode.S3 => "[bold] [dim][[1]][/] [white]S3[/]  [dim][[2]][/] SSM  [dim][[3]][/] SQS  [dim][[4]][/] SNS[/]",
            ServiceMode.Ssm => "[bold] [dim][[1]][/] S3  [dim][[2]][/] [white]SSM[/]  [dim][[3]][/] SQS  [dim][[4]][/] SNS[/]",
            ServiceMode.Sqs => "[bold] [dim][[1]][/] S3  [dim][[2]][/] SSM  [dim][[3]][/] [white]SQS[/]  [dim][[4]][/] SNS[/]",
            ServiceMode.Sns => "[bold] [dim][[1]][/] S3  [dim][[2]][/] SSM  [dim][[3]][/] SQS  [dim][[4]][/] [white]SNS[/][/]",
            _ => "[bold] [dim][[1]][/] [white]S3[/]  [dim][[2]][/] SSM  [dim][[3]][/] SQS  [dim][[4]][/] SNS[/]"
        };
        content.AppendLine($"  {tabs}");
        content.AppendLine();

        // Panel content
        _panels[_mode].Render(content);
        content.AppendLine();

        // Help bar
        var help = _panels[_mode].HelpText;
        help += _containerStatus.Contains("running") ? " · x stop" : " · s start";
        help += " · q quit";
        content.AppendLine($"[grey]{help}[/]");

        return new Panel(new Markup(content.ToString()))
        {
            Padding = new Padding(0, 0, 0, 0),
            Border = BoxBorder.None
        };
    }

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

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        foreach (var panel in _panels.Values)
            panel.Dispose();
        _dockerClient?.Dispose();
    }
}
