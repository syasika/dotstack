using Docker.DotNet;
using Docker.DotNet.Models;
using DotStack.Cli.Abstractions;
using DotStack.Core.Configuration;
using Spectre.Console;
using Config = DotStack.Core.Configuration.Config;

namespace DotStack.Cli.Infrastructure;

public class ContainerInitializer
{
    private readonly IAnsiConsole _console;
    private readonly IDockerClientFactory _dockerFactory;

    public ContainerInitializer(IAnsiConsole console, IDockerClientFactory dockerFactory)
    {
        _console = console;
        _dockerFactory = dockerFactory;
    }

    public virtual void EnsureContainer(Config cfg, CancellationToken ct)
    {
        using var docker = _dockerFactory.CreateClient();

        while (true)
        {
            try
            {
                docker
                    .Containers.InspectContainerAsync(cfg.ContainerName, ct)
                    .GetAwaiter()
                    .GetResult();
                docker
                    .Containers.StartContainerAsync(cfg.ContainerName, null, ct)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (DockerContainerNotFoundException) { }

            try
            {
                _console.MarkupLine($"Pulling image '[bold]{cfg.ImageName}[/]'...");
                docker
                    .Images.CreateImageAsync(
                        new ImagesCreateParameters { FromImage = cfg.ImageName },
                        null,
                        new Progress<JSONMessage>(),
                        ct
                    )
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[yellow]⚠ Failed to pull image: {ex.Message}[/]");
                var newImage = _console.Prompt(new TextPrompt<string>("Enter a different Docker image"));
                if (string.IsNullOrEmpty(newImage))
                    throw;
                cfg = cfg with { ImageName = newImage };
                continue;
            }

            var hostPort = cfg.Port;
            docker
                .Containers.CreateContainerAsync(
                    new CreateContainerParameters
                    {
                        Image = cfg.ImageName,
                        Name = cfg.ContainerName,
                        ExposedPorts = new Dictionary<string, EmptyStruct>
                        {
                            [$"{hostPort}/tcp"] = new EmptyStruct(),
                        },
                        HostConfig = new HostConfig
                        {
                            PortBindings = new Dictionary<
                                string,
                                IList<PortBinding>
                            >
                            {
                                [$"{hostPort}/tcp"] =
                                [
                                    new PortBinding
                                    {
                                        HostPort = hostPort,
                                        HostIP = "0.0.0.0",
                                    },
                                ],
                            },
                        },
                    },
                    ct
                )
                .GetAwaiter()
                .GetResult();

            docker
                .Containers.StartContainerAsync(cfg.ContainerName, null, ct)
                .GetAwaiter()
                .GetResult();
            return;
        }
    }
}
