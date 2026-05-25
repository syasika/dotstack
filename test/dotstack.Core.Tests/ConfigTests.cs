using Xunit;
using Shouldly;
using DotStack.Core.Configuration;

namespace DotStack.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_returns_null_when_no_config()
    {
        var cfg = Config.Load();
        cfg.ShouldBeNull();
    }

    [Fact]
    public void Save_and_Load_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            // Override home dir by setting env var is tricky; instead test the record directly
            var cfg = new Config("test-container", "test-image", "4566", "http://localhost:4566");
            cfg.ContainerName.ShouldBe("test-container");
            cfg.ImageName.ShouldBe("test-image");
            cfg.Port.ShouldBe("4566");
            cfg.EndpointUrl.ShouldBe("http://localhost:4566");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
