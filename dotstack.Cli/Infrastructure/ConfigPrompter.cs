using DotStack.Core.Configuration;
using Spectre.Console;

namespace DotStack.Cli.Infrastructure;

public class ConfigPrompter
{
    private readonly IAnsiConsole _console;

    public ConfigPrompter(IAnsiConsole console)
    {
        _console = console;
    }

    public virtual Config PromptSetup()
    {
        var containerName = _console.Prompt(new TextPrompt<string>("Container name").DefaultValue("ministack"));
        var imageName = _console.Prompt(new TextPrompt<string>("Docker image").DefaultValue("ministackorg/ministack"));
        var port = "4566";
        return new Config(containerName, imageName, port, $"http://localhost:{port}");
    }
}
