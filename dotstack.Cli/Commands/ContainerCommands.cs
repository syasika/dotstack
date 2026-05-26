using Docker.DotNet;
using DotStack.Core.Configuration;
using Spectre.Console;
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
        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                AnsiConsole.MarkupLine("[yellow]Not initialized. Run 'dotstack init' first.[/]");
                return 0;
            }

            using var docker = new DockerClientConfiguration().CreateClient();
            try
            {
                var ci = docker
                    .Containers.InspectContainerAsync(cfg.ContainerName, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                var color = ci.State.Status == "running" ? "green" : "yellow";
                var symbol = ci.State.Status == "running" ? "● running" : $"● {ci.State.Status}";
                AnsiConsole.MarkupLine($"[bold]Container:[/]  {cfg.ContainerName}");
                AnsiConsole.MarkupLine($"[bold]Image:[/]     {cfg.ImageName}");
                AnsiConsole.MarkupLine($"[bold]Status:[/]    [{color} bold]{symbol}[/]");
                if (ci.State.Status == "running")
                    AnsiConsole.MarkupLine($"[bold]Started:[/]   {ci.State.StartedAt}");
            }
            catch (DockerContainerNotFoundException)
            {
                AnsiConsole.MarkupLine($"[yellow]Container '{cfg.ContainerName}' not found[/]");
            }
            return 0;
        }
    }

    public class StartCommand : Command<CommandSettings>
    {
        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                AnsiConsole.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = new DockerClientConfiguration().CreateClient();
            docker
                .Containers.StartContainerAsync(cfg.ContainerName, null, cancellationToken)
                .GetAwaiter()
                .GetResult();
            AnsiConsole.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' started"
            );
            return 0;
        }
    }

    public class StopCommand : Command<CommandSettings>
    {
        protected override int Execute(
            CommandContext context,
            CommandSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                AnsiConsole.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = new DockerClientConfiguration().CreateClient();
            docker
                .Containers.StopContainerAsync(cfg.ContainerName, null, cancellationToken)
                .GetAwaiter()
                .GetResult();
            AnsiConsole.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' stopped"
            );
            return 0;
        }
    }

    public class RemoveCommand : Command<RemoveSettings>
    {
        protected override int Execute(
            CommandContext context,
            RemoveSettings settings,
            CancellationToken cancellationToken
        )
        {
            var cfg = Config.Load();
            if (cfg is null)
            {
                AnsiConsole.MarkupLine("[red]Not initialized. Run 'dotstack init' first.[/]");
                return 1;
            }
            using var docker = new DockerClientConfiguration().CreateClient();
            docker
                .Containers.RemoveContainerAsync(
                    cfg.ContainerName,
                    new Docker.DotNet.Models.ContainerRemoveParameters { Force = settings.Force },
                    cancellationToken
                )
                .GetAwaiter()
                .GetResult();
            Config.Remove();
            AnsiConsole.MarkupLine(
                $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' removed"
            );
            return 0;
        }
    }
}
