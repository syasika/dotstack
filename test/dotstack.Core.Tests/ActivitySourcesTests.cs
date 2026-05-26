using DotStack.Core.Telemetry;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class ActivitySourcesTests
{
    [Fact]
    public void DotStack_source_has_correct_name_and_version()
    {
        ActivitySources.DotStack.Name.ShouldBe("DotStack.Core");
        ActivitySources.DotStack.Version.ShouldBe("1.0.0");
    }
}
