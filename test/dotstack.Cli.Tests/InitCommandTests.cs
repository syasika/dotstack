using Docker.DotNet;
using Docker.DotNet.Models;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using DotStack.Cli.Infrastructure;
using DotStack.Core.Configuration;
using FakeItEasy;
using Config = DotStack.Core.Configuration.Config;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace DotStack.Cli.Tests;

public class InitCommandTests
{
    private readonly TestConsole _console;
    private readonly IDockerClientFactory _dockerFactory;
    private readonly IDockerClient _dockerClient;
    private readonly IContainerOperations _containers;
    private readonly ConfigPrompter _prompter;
    private readonly ContainerInitializer _initializer;

    public InitCommandTests()
    {
        _console = new TestConsole();
        _dockerClient = A.Fake<IDockerClient>();
        _containers = A.Fake<IContainerOperations>();
        _dockerFactory = A.Fake<IDockerClientFactory>();
        _prompter = A.Fake<ConfigPrompter>(_ => { });
        _initializer = A.Fake<ContainerInitializer>(_ => { });

        A.CallTo(() => _dockerClient.Containers).Returns(_containers);
        A.CallTo(() => _dockerFactory.CreateClient()).Returns(_dockerClient);
    }

    private static IDisposable SetupConfig(
        string containerName = "existing-container",
        string imageName = "ministackorg/ministack")
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

    [Fact]
    public void InitCommand_already_running_returns_success()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("existing-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "running" },
            });

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("already running");
    }

    [Fact]
    public void InitCommand_exited_container_starts_it()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("existing-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "exited" },
            });

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(0);
        A.CallTo(() => _containers.StartContainerAsync(
                "existing-container", A<ContainerStartParameters?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("started");
    }

    [Fact]
    public void InitCommand_paused_container_unpauses()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("existing-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "paused" },
            });

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(0);
        A.CallTo(() => _containers.UnpauseContainerAsync(
                "existing-container", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("unpaused");
    }

    [Fact]
    public void InitCommand_unexpected_state_returns_error()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("existing-container", A<CancellationToken>._))
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "dead" },
            });

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Unexpected container state");
    }

    [Fact]
    public void InitCommand_config_exists_but_container_not_found_runs_setup()
    {
        using var scope = SetupConfig();
        A.CallTo(() => _containers.InspectContainerAsync("existing-container", A<CancellationToken>._))
            .Throws(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        Config expectedCfg = new Config("new-container", "new-image", "4566", "http://localhost:4566");
        A.CallTo(() => _prompter.PromptSetup()).Returns(expectedCfg);

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(0);
        A.CallTo(() => _initializer.EnsureContainer(expectedCfg, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void InitCommand_no_config_runs_full_setup()
    {
        using var scope = TempHomeScope().Scope;
        // No config file written — Config.Load() returns null
        Config expectedCfg = new Config("new-container", "ministackorg/ministack", "4566", "http://localhost:4566");
        A.CallTo(() => _prompter.PromptSetup()).Returns(expectedCfg);

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        var result = cmd.Execute(new InitCommand.Settings());

        result.ShouldBe(0);
        A.CallTo(() => _prompter.PromptSetup()).MustHaveHappenedOnceExactly();
        A.CallTo(() => _initializer.EnsureContainer(expectedCfg, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("No running ministack container");
        _console.Output.ShouldContain("is running");
    }

    // ---- Cancellation token forwarding ----

    [Fact]
    public void InitCommand_running_container_forwards_cancellation_token_to_Docker()
    {
        using var scope = SetupConfig();
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        A.CallTo(() => _containers.InspectContainerAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string _, CancellationToken ct) => captured = ct)
            .Returns(new ContainerInspectResponse
            {
                State = new ContainerState { Status = "running" },
            });

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        cmd.Execute(new InitCommand.Settings(), cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public void InitCommand_setup_forwards_cancellation_token_to_initializer()
    {
        using var scope = TempHomeScope().Scope;
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        Config expectedCfg = new Config("new-container", "ministackorg/ministack", "4566", "http://localhost:4566");
        A.CallTo(() => _prompter.PromptSetup()).Returns(expectedCfg);
        A.CallTo(() => _initializer.EnsureContainer(A<Config>._, A<CancellationToken>._))
            .Invokes((Config _, CancellationToken ct) => captured = ct);

        var cmd = new InitCommand(_console, _dockerFactory, _prompter, _initializer);
        cmd.Execute(new InitCommand.Settings(), cts.Token);

        captured.ShouldBe(cts.Token);
    }
}
