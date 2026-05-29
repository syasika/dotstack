using Docker.DotNet;
using Docker.DotNet.Models;
using DotStack.Cli.Abstractions;
using DotStack.Core.Configuration;
using Spectre.Console;
using Config = DotStack.Core.Configuration.Config;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public static class ContainerCommands
{
    public sealed class RemoveSettings : CommandSettings
    {
        [CommandOption("-f|--force")]
        public bool Force { get; set; }
    }

    public class StatusCommand : Command<CommandSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IDockerClientFactory _dockerFactory;

        public StatusCommand(IAnsiConsole console, IDockerClientFactory dockerFactory)
        {
            _console = console;
            _dockerFactory = dockerFactory;
        }

        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                _console.MarkupLine("[yellow]Not initialized. Run 'dotstack init' first.[/]");
                return 0;
            }

            using var docker = _dockerFactory.CreateClient();
            try
            {
                var ci = docker
                    .Containers.InspectContainerAsync(cfg.ContainerName, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                var color = ci.State.Status == "running" ? "green" : "yellow";
                var symbol = ci.State.Status == "running" ? "● running" : $"● {ci.State.Status}";
                _console.MarkupLine($"[bold]Container:[/]  {cfg.ContainerName}");
                _console.MarkupLine($"[bold]Image:[/]     {cfg.ImageName}");
                _console.MarkupLine($"[bold]Status:[/]    [{color} bold]{symbol}[/]");
                if (ci.State.Status == "running")
                    _console.MarkupLine($"[bold]Started:[/]   {ci.State.StartedAt}");
            }
            catch (DockerContainerNotFoundException)
            {
                _console.MarkupLine($"[yellow]Container '{cfg.ContainerName}' not found[/]");
            }
            return 0;
        }
    }

    public class StartCommand : Command<CommandSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IDockerClientFactory _dockerFactory;

        public StartCommand(IAnsiConsole console, IDockerClientFactory dockerFactory)
        {
            _console = console;
            _dockerFactory = dockerFactory;
        }

        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                _console.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = _dockerFactory.CreateClient();
            docker
                .Containers.StartContainerAsync(cfg.ContainerName, null, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' started"
            );
            return 0;
        }
    }

    public class StopCommand : Command<CommandSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IDockerClientFactory _dockerFactory;

        public StopCommand(IAnsiConsole console, IDockerClientFactory dockerFactory)
        {
            _console = console;
            _dockerFactory = dockerFactory;
        }

        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                _console.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = _dockerFactory.CreateClient();
            docker
                .Containers.StopContainerAsync(cfg.ContainerName, null, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' stopped"
            );
            return 0;
        }
    }

    public class RemoveCommand : Command<RemoveSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IDockerClientFactory _dockerFactory;

        public RemoveCommand(IAnsiConsole console, IDockerClientFactory dockerFactory)
        {
            _console = console;
            _dockerFactory = dockerFactory;
        }

        protected override int Execute(
            CommandContext context,
            RemoveSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                _console.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = _dockerFactory.CreateClient();
            docker
                .Containers.RemoveContainerAsync(
                    cfg.ContainerName,
                    new ContainerRemoveParameters { Force = settings.Force },
                    cancellationToken
                )
                .GetAwaiter()
                .GetResult();
            Config.Remove();
            _console.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' removed"
            );
            return 0;
        }
    }
}
