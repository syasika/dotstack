using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public class EndpointSettings : CommandSettings
{
    [CommandOption("-e|--endpoint-url")]
    public string EndpointUrl { get; set; } = "http://localhost:4566";
}
