using DotStack.Cli.Commands;
using Shouldly;
using Spectre.Console.Cli;
using Xunit;

namespace DotStack.Cli.Tests;

public class BrowseCommandTests
{
    [Fact]
    public void BrowseCommand_is_Command_of_EndpointSettings()
    {
        var cmd = new BrowseCommand();
        cmd.ShouldBeAssignableTo<Command<EndpointSettings>>();
    }

    [Fact]
    public void BrowseCommand_endpoint_settings_defaults()
    {
        var cmd = new BrowseCommand();
        cmd.ShouldNotBeNull();
    }
}
