using Docker.DotNet;
using DotStack.Cli.Abstractions;

namespace DotStack.Cli.Infrastructure;

public sealed class DockerClientFactory : IDockerClientFactory
{
    public IDockerClient CreateClient() =>
        new DockerClientConfiguration().CreateClient();
}
