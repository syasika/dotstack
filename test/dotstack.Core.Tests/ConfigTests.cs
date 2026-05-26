using DotStack.Core.Configuration;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class ConfigTests
{
    private static (IDisposable Scope, string HomeDir) TempHomeScope()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var origHome = Environment.GetEnvironmentVariable("HOME");
        var origProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        Environment.SetEnvironmentVariable("HOME", tempDir);
        Environment.SetEnvironmentVariable("USERPROFILE", tempDir);

        var capturedDir = tempDir;
        return (
            new DisposableAction(() =>
            {
                Environment.SetEnvironmentVariable("HOME", origHome);
                Environment.SetEnvironmentVariable("USERPROFILE", origProfile);
                if (Directory.Exists(capturedDir))
                    Directory.Delete(capturedDir, true);
            }),
            tempDir
        );
    }

    [Fact]
    public void Load_returns_null_when_no_config()
    {
        var (scope, _) = TempHomeScope();
        using (scope)
        {
            var cfg = Config.Load();
            cfg.ShouldBeNull();
        }
    }

    [Fact]
    public void Record_properties_are_set()
    {
        var cfg = new Config("test-container", "test-image", "4566", "http://localhost:4566");
        cfg.ContainerName.ShouldBe("test-container");
        cfg.ImageName.ShouldBe("test-image");
        cfg.Port.ShouldBe("4566");
        cfg.EndpointUrl.ShouldBe("http://localhost:4566");
    }

    [Fact]
    public void Save_creates_config_file()
    {
        var (scope, homeDir) = TempHomeScope();
        using (scope)
        {
            var cfg = new Config("c", "i", "4566", "http://localhost:4566");
            cfg.Save();

            var configFile = Path.Combine(homeDir, ".dotstack", "config.json");
            File.Exists(configFile).ShouldBeTrue();
        }
    }

    [Fact]
    public void Save_and_Load_roundtrip_values()
    {
        var (scope, _) = TempHomeScope();
        using (scope)
        {
            var original = new Config("my-container", "my-image", "9999", "http://localhost:9999");
            original.Save();

            var loaded = Config.Load();

            loaded.ShouldNotBeNull();
            loaded.ContainerName.ShouldBe("my-container");
            loaded.ImageName.ShouldBe("my-image");
            loaded.Port.ShouldBe("9999");
            loaded.EndpointUrl.ShouldBe("http://localhost:9999");
        }
    }

    [Fact]
    public void Remove_deletes_config_file()
    {
        var (scope, homeDir) = TempHomeScope();
        using (scope)
        {
            var cfg = new Config("c", "i", "4566", "http://localhost:4566");
            cfg.Save();

            var configFile = Path.Combine(homeDir, ".dotstack", "config.json");
            File.Exists(configFile).ShouldBeTrue();

            Config.Remove();

            File.Exists(configFile).ShouldBeFalse();
        }
    }

    [Fact]
    public void Remove_noop_when_no_config_file()
    {
        var (scope, _) = TempHomeScope();
        using (scope)
        {
            // Should not throw
            Config.Remove();
        }
    }
}

internal class DisposableAction(Action action) : IDisposable
{
    public void Dispose() => action();
}
