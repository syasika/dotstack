using Xunit;
using Shouldly;

namespace DotStack.Tui.Tests;

public class DashboardTests
{
    [Fact]
    public void ServiceMode_has_four_values()
    {
        var names = Enum.GetNames<ServiceMode>();
        names.Length.ShouldBe(4);
        names.ShouldContain("S3");
        names.ShouldContain("Ssm");
        names.ShouldContain("Sqs");
        names.ShouldContain("Sns");
    }
}
