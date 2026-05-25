using Spectre.Console;
using Spectre.Console.Cli;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DotStack.Cli.Commands;

public class InitCommand : Command<InitCommand.Settings>
{
    public class Settings : CommandSettings { }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var cfg = Core.Configuration.Config.Load();

        if (cfg is not null)
        {
            using var docker = new DockerClientConfiguration().CreateClient();
            try
            {
                var ci = docker.Containers.InspectContainerAsync(cfg.ContainerName, cancellationToken)
                    .GetAwaiter().GetResult();

                switch (ci.State.Status)
                {
                    case "running":
                        AnsiConsole.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' is already running (image: {cfg.ImageName})");
                        return 0;

                    case "exited":
                        AnsiConsole.MarkupLine(
                            $"Container '[bold]{cfg.ContainerName}[/]' exists but is stopped. Starting...");
                        docker.Containers.StartContainerAsync(cfg.ContainerName, null, cancellationToken)
                            .GetAwaiter().GetResult();
                        AnsiConsole.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' started");
                        return 0;

                    case "paused":
                        AnsiConsole.MarkupLine(
                            $"Container '[bold]{cfg.ContainerName}[/]' is paused. Unpausing...");
                        docker.Containers.UnpauseContainerAsync(cfg.ContainerName, cancellationToken)
                            .GetAwaiter().GetResult();
                        AnsiConsole.MarkupLine(
                            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' unpaused");
                        return 0;

                    default:
                        AnsiConsole.MarkupLine(
                            $"[red]Unexpected container state: {ci.State.Status}[/]");
                        return 1;
                }
            }
            catch (Docker.DotNet.DockerContainerNotFoundException) { }
        }

        AnsiConsole.MarkupLine("[yellow]No running ministack container found. Let's set one up.[/]");
        cfg = PromptSetup();
        EnsureContainer(cfg, cancellationToken);

        cfg.Save();
        AnsiConsole.MarkupLine(
            $"[green bold]✓[/] Container '[bold]{cfg.ContainerName}[/]' is running (image: {cfg.ImageName})");
        return 0;
    }

    private static Core.Configuration.Config PromptSetup()
    {
        var containerName = AnsiConsole.Ask("Container name", "ministack");
        var imageName = AnsiConsole.Ask("Docker image", "ministackorg/ministack");
        var port = "4566";
        return new Core.Configuration.Config(containerName, imageName, port, $"http://localhost:{port}");
    }

    private static void EnsureContainer(Core.Configuration.Config cfg, CancellationToken ct)
    {
        using var docker = new DockerClientConfiguration().CreateClient();

        while (true)
        {
            try
            {
                docker.Containers.InspectContainerAsync(cfg.ContainerName, ct)
                    .GetAwaiter().GetResult();
                docker.Containers.StartContainerAsync(cfg.ContainerName, null, ct)
                    .GetAwaiter().GetResult();
                return;
            }
            catch (Docker.DotNet.DockerContainerNotFoundException) { }

            try
            {
                AnsiConsole.MarkupLine($"Pulling image '[bold]{cfg.ImageName}[/]'...");
                docker.Images.CreateImageAsync(
                    new Docker.DotNet.Models.ImagesCreateParameters { FromImage = cfg.ImageName },
                    null, new Progress<Docker.DotNet.Models.JSONMessage>(), ct)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Failed to pull image: {ex.Message}[/]");
                var newImage = AnsiConsole.Ask<string>("Enter a different Docker image");
                if (string.IsNullOrEmpty(newImage)) throw;
                cfg = cfg with { ImageName = newImage };
                continue;
            }

            var hostPort = cfg.Port;
            docker.Containers.CreateContainerAsync(new Docker.DotNet.Models.CreateContainerParameters
            {
                Image = cfg.ImageName,
                Name = cfg.ContainerName,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    [$"{hostPort}/tcp"] = new EmptyStruct()
                },
                HostConfig = new Docker.DotNet.Models.HostConfig
                {
                    PortBindings = new Dictionary<string, IList<Docker.DotNet.Models.PortBinding>>
                    {
                        [$"{hostPort}/tcp"] =
                        [
                            new Docker.DotNet.Models.PortBinding { HostPort = hostPort, HostIP = "0.0.0.0" }
                        ]
                    }
                }
            }, ct).GetAwaiter().GetResult();

            docker.Containers.StartContainerAsync(cfg.ContainerName, null, ct)
                .GetAwaiter().GetResult();
            return;
        }
    }
}
