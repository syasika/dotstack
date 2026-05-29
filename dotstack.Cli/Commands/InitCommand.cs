using DotStack.Cli.Abstractions;
using DotStack.Cli.Infrastructure;
using DotStack.Core;
using DotStack.Core.Configuration;
using Docker.DotNet;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public class InitCommand : Command<InitCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IDockerClientFactory _dockerFactory;
    private readonly ConfigPrompter _prompter;
    private readonly ContainerInitializer _initializer;

    public class Settings : CommandSettings
    {
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; set; }
    }

    public InitCommand(IAnsiConsole console, IDockerClientFactory dockerFactory)
    {
        _console = console;
        _dockerFactory = dockerFactory;
        _prompter = new ConfigPrompter(console);
        _initializer = new ContainerInitializer(console, dockerFactory);
    }

    // Internal constructor for test injection
    internal InitCommand(
        IAnsiConsole console,
        IDockerClientFactory dockerFactory,
        ConfigPrompter prompter,
        ContainerInitializer initializer)
    {
        _console = console;
        _dockerFactory = dockerFactory;
        _prompter = prompter;
        _initializer = initializer;
    }

    protected override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        VerboseConfig.Enabled = settings.Verbose;
        var cfg = Config.Load();

        if (cfg is not null)
        {
            using var docker = _dockerFactory.CreateClient();
            try
            {
                var ci = docker
                    .Containers.InspectContainerAsync(cfg.ContainerName, cancellationToken)
                    .GetAwaiter()
                    .GetResult();

                switch (ci.State.Status)
                {
                    case "running":
                        _console.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' is already running (image: {cfg.ImageName})"
                        );
                        return 0;

                    case "exited":
                        _console.MarkupLine(
                            $"Container '[bold]{cfg.ContainerName}[/]' exists but is stopped. Starting..."
                        );
                        docker
                            .Containers.StartContainerAsync(
                                cfg.ContainerName,
                                null,
                                cancellationToken
                            )
                            .GetAwaiter()
                            .GetResult();
                        _console.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' started"
                        );
                        return 0;

                    case "paused":
                        _console.MarkupLine(
                            $"Container '[bold]{cfg.ContainerName}[/]' is paused. Unpausing..."
                        );
                        docker
                            .Containers.UnpauseContainerAsync(cfg.ContainerName, cancellationToken)
                            .GetAwaiter()
                            .GetResult();
                        _console.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' unpaused"
                        );
                        return 0;

                    default:
                        _console.MarkupLine(
                            $"[red]Unexpected container state: {ci.State.Status}[/]"
                        );
                        return 1;
                }
            }
            catch (DockerContainerNotFoundException) { }
        }

        _console.MarkupLine(
            "[yellow]No running ministack container found. Let's set one up.[/]"
        );
        cfg = _prompter.PromptSetup();
        _initializer.EnsureContainer(cfg, cancellationToken);

        cfg.Save();
        _console.MarkupLine(
            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' is running (image: {cfg.ImageName})"
        );
        return 0;
    }
}
