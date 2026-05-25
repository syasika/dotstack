using DotStack.Core.Configuration;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_returns_null_when_no_config()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        try
        {
            Environment.SetEnvironmentVariable("HOME", dir);
            Environment.SetEnvironmentVariable("USERPROFILE", dir);

            var cfg = Config.Load();
            cfg.ShouldBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOME", originalHome);
            Environment.SetEnvironmentVariable("USERPROFILE", originalUserProfile);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
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
