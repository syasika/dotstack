using Docker.DotNet;

namespace DotStack.Cli.Abstractions;

public interface IDockerClientFactory
{
    IDockerClient CreateClient();
}
