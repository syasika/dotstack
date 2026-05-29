using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using DotStack.Core.Configuration;
using FakeItEasy;
using Shouldly;
using Spectre.Console.Testing;
using Spectre.Console.Cli;
using Xunit;
using Config = DotStack.Core.Configuration.Config;

namespace DotStack.Cli.Tests;

public class ContainerCommandsTests
{
    private readonly TestConsole _console;
    private readonly IDockerClientFactory _dockerFactory;
    private readonly IDockerClient _dockerClient;
    private readonly IContainerOperations _containers;

    public ContainerCommandsTests()
    {
        _console = new TestConsole();
        _dockerClient = A.Fake<IDockerClient>();
        _containers = A.Fake<IContainerOperations>();
        _dockerFactory = A.Fake<IDockerClientFactory>();
        A.CallTo(() => _dockerClient.Containers).Returns(_containers);
        A.CallTo(() => _dockerFactory.CreateClient()).Returns(_dockerClient);
    }

    private static IDisposable SetupConfig(string containerName = "test-container", string imageName = "test-image")
    {
        var (scope, homeDir) = TempHomeScope();
        var cfg = new Config(containerName, imageName, "4566", "http://localhost:4566");
        cfg.Save();
        return scope;
    }

    private static (IDisposable Scope, string HomeDir) TempHomeScope()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var origHome = Environment.GetEnvironmentVariable("HOME");
        var origProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        Environment.SetEnvironmentVariable("HOME", tempDir);
        Environment.SetEnvironmentVariable("USERPROFILE", tempDir);

        return (
            new DisposableAction(() =>
            {
                Environment.SetEnvironmentVariable("HOME", origHome);
                Environment.SetEnvironmentVariable("USERPROFILE", origProfile);
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }),
            tempDir
        );
    }

    // ---- StatusCommand ----

    [Fact]
    public void StatusCommand_not_initialized_shows_message()
    {
        using var scope = TempHomeScope().Scope;
        var cmd = new ContainerCommands.StatusCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Not initialized");
    }

    [Fact]
    public void StatusCommand_running_container_shows_status()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("test-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState
                {
                    Status = "running",
                    StartedAt = DateTime.UtcNow.ToString("O"),
                },
            });

        var cmd = new ContainerCommands.StatusCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("running");
        _console.Output.ShouldContain("test-container");
    }

    [Fact]
    public void StatusCommand_stopped_container_shows_stopped()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("test-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "exited" },
            });

        var cmd = new ContainerCommands.StatusCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("exited");
    }

    [Fact]
    public void StatusCommand_container_not_found_shows_not_found_message()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("test-container", A<CancellationToken>._))
            .Throws(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        var cmd = new ContainerCommands.StatusCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("not found");
    }

    // ---- StartCommand ----

    [Fact]
    public void StartCommand_starts_container()
    {
        using var scope = SetupConfig();

        var cmd = new ContainerCommands.StartCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        A.CallTo(() => _containers.StartContainerAsync(
                "test-container", A<ContainerStartParameters?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("started");
    }

    [Fact]
    public void StartCommand_not_initialized_returns_error()
    {
        using var scope = TempHomeScope().Scope;
        var cmd = new ContainerCommands.StartCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Not initialized");
    }

    // ---- StopCommand ----

    [Fact]
    public void StopCommand_stops_container()
    {
        using var scope = SetupConfig();

        var cmd = new ContainerCommands.StopCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(0);
        A.CallTo(() => _containers.StopContainerAsync(
                "test-container", A<ContainerStopParameters?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("stopped");
    }

    [Fact]
    public void StopCommand_not_initialized_returns_error()
    {
        using var scope = TempHomeScope().Scope;
        var cmd = new ContainerCommands.StopCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ConcreteCommandSettings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Not initialized");
    }

    // ---- RemoveCommand ----

    [Fact]
    public void RemoveCommand_removes_container()
    {
        using var scope = SetupConfig();

        var cmd = new ContainerCommands.RemoveCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ContainerCommands.RemoveSettings());

        result.ShouldBe(0);
        A.CallTo(() => _containers.RemoveContainerAsync(
                "test-container",
                A<ContainerRemoveParameters>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("removed");
    }

    [Fact]
    public void RemoveCommand_force_passes_force_flag()
    {
        using var scope = SetupConfig();

        var cmd = new ContainerCommands.RemoveCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ContainerCommands.RemoveSettings { Force = true });

        result.ShouldBe(0);
        A.CallTo(() => _containers.RemoveContainerAsync(
                "test-container",
                A<ContainerRemoveParameters>.That.Matches(p => p.Force == true),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void RemoveCommand_not_initialized_returns_error()
    {
        using var scope = TempHomeScope().Scope;
        var cmd = new ContainerCommands.RemoveCommand(_console, _dockerFactory);
        var result = cmd.Execute(new ContainerCommands.RemoveSettings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Not initialized");
    }
}

/// <summary>
/// Disposable action wrapper for test cleanup.
/// </summary>
internal class DisposableAction(Action action) : IDisposable
{
    public void Dispose() => action();
}
