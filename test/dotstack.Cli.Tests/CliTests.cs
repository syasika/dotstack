using Xunit;
using Shouldly;

namespace DotStack.Cli.Tests;

public class CliTests
{
    [Fact]
    public void EndpointSettings_default_url()
    {
        var settings = new DotStack.Cli.Commands.EndpointSettings();
        settings.EndpointUrl.ShouldBe("http://localhost:4566");
    }
}
