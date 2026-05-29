using DotStack.Cli.Commands;
using Shouldly;
using Xunit;

namespace DotStack.Cli.Tests;

public class EndpointSettingsTests
{
    [Fact]
    public void EndpointUrl_defaults_to_localhost_4566()
    {
        var settings = new EndpointSettings();
        settings.EndpointUrl.ShouldBe("http://localhost:4566");
    }

    [Fact]
    public void Verbose_defaults_to_false()
    {
        var settings = new EndpointSettings();
        settings.Verbose.ShouldBeFalse();
    }

    [Fact]
    public void Can_set_EndpointUrl()
    {
        var settings = new EndpointSettings { EndpointUrl = "http://custom:9999" };
        settings.EndpointUrl.ShouldBe("http://custom:9999");
    }

    [Fact]
    public void Can_set_Verbose()
    {
        var settings = new EndpointSettings { Verbose = true };
        settings.Verbose.ShouldBeTrue();
    }
}
