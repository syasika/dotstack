using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using FakeItEasy;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace DotStack.Cli.Tests;

public class SsmCommandsTests
{
    private readonly TestConsole _console;
    private readonly IAwsClientFactory _factory;
    private readonly IAmazonSimpleSystemsManagement _ssm;

    public SsmCommandsTests()
    {
        _console = new TestConsole();
        _ssm = A.Fake<IAmazonSimpleSystemsManagement>();
        _factory = A.Fake<IAwsClientFactory>();
        A.CallTo(() => _factory.CreateSsmClient(A<string>._)).Returns(_ssm);
    }

    // ---- LsCommand ----

    [Fact]
    public void LsCommand_no_parameters_shows_empty()
    {
        A.CallTo(() => _ssm.DescribeParametersAsync(A<DescribeParametersRequest>._, A<CancellationToken>._))
            .Returns(new DescribeParametersResponse { Parameters = [] });

        var cmd = new SsmCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("No parameters");
    }

    [Fact]
    public void LsCommand_shows_parameters()
    {
        A.CallTo(() => _ssm.DescribeParametersAsync(A<DescribeParametersRequest>._, A<CancellationToken>._))
            .Returns(new DescribeParametersResponse
            {
                Parameters =
                [
                    new ParameterMetadata { Name = "/app/db/host", Type = "String", Version = 1 },
                    new ParameterMetadata { Name = "/app/db/port", Type = "String", Version = 2 },
                ],
            });

        var cmd = new SsmCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Parameters (2)");
        _console.Output.ShouldContain("/app/db/host");
        _console.Output.ShouldContain("/app/db/port");
    }

    // ---- GetCommand ----

    [Fact]
    public void GetCommand_shows_parameter_details()
    {
        A.CallTo(() => _ssm.GetParameterAsync(
                A<GetParameterRequest>.That.Matches(r => r.Name == "/app/key"),
                A<CancellationToken>._))
            .Returns(new GetParameterResponse
            {
                Parameter = new Amazon.SimpleSystemsManagement.Model.Parameter
                {
                    Name = "/app/key",
                    Type = "SecureString",
                    Value = "secret-value",
                    Version = 3,
                },
            });

        var cmd = new SsmCommands.GetCommand(_console, _factory);
        var result = cmd.Execute(new SsmCommands.NameSettings { Name = "/app/key" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("/app/key");
        _console.Output.ShouldContain("SecureString");
        _console.Output.ShouldContain("secret-value");
        _console.Output.ShouldContain("v3");
    }

    // ---- PutCommand ----

    [Fact]
    public void PutCommand_saves_parameter()
    {
        var cmd = new SsmCommands.PutCommand(_console, _factory);
        var result = cmd.Execute(new SsmCommands.PutSettings { Name = "/app/key", Value = "val" });

        result.ShouldBe(0);
        A.CallTo(() => _ssm.PutParameterAsync(
                A<PutParameterRequest>.That.Matches(r =>
                    r.Name == "/app/key" && r.Value == "val" && r.Type == "String"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("saved");
    }

    [Fact]
    public void PutCommand_with_custom_type()
    {
        var cmd = new SsmCommands.PutCommand(_console, _factory);
        var result = cmd.Execute(new SsmCommands.PutSettings
        {
            Name = "/app/sec",
            Value = "secret",
            Type = "SecureString",
        });

        result.ShouldBe(0);
        A.CallTo(() => _ssm.PutParameterAsync(
                A<PutParameterRequest>.That.Matches(r => r.Type == "SecureString"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    // ---- RmCommand ----

    [Fact]
    public void RmCommand_deletes_parameter()
    {
        var cmd = new SsmCommands.RmCommand(_console, _factory);
        var result = cmd.Execute(new SsmCommands.NameSettings { Name = "/app/old-key" });

        result.ShouldBe(0);
        A.CallTo(() => _ssm.DeleteParameterAsync(
                A<DeleteParameterRequest>.That.Matches(r => r.Name == "/app/old-key"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("deleted");
    }

    // ---- Cancellation token forwarding ----

    [Fact]
    public void LsCommand_forwards_cancellation_token_to_SDK()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        A.CallTo(() => _ssm.DescribeParametersAsync(A<DescribeParametersRequest>._, A<CancellationToken>._))
            .Invokes((DescribeParametersRequest _, CancellationToken ct) => captured = ct)
            .Returns(new DescribeParametersResponse { Parameters = [] });

        var cmd = new SsmCommands.LsCommand(_console, _factory);
        cmd.Execute(new EndpointSettings(), cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public void GetCommand_forwards_cancellation_token_to_SDK()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        A.CallTo(() => _ssm.GetParameterAsync(A<GetParameterRequest>._, A<CancellationToken>._))
            .Invokes((GetParameterRequest _, CancellationToken ct) => captured = ct)
            .Returns(new GetParameterResponse
            {
                Parameter = new Amazon.SimpleSystemsManagement.Model.Parameter { Name = "/key", Value = "v" },
            });

        var cmd = new SsmCommands.GetCommand(_console, _factory);
        cmd.Execute(new SsmCommands.NameSettings { Name = "/key" }, cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public void PutCommand_forwards_cancellation_token_to_SDK()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        A.CallTo(() => _ssm.PutParameterAsync(A<PutParameterRequest>._, A<CancellationToken>._))
            .Invokes((PutParameterRequest _, CancellationToken ct) => captured = ct);

        var cmd = new SsmCommands.PutCommand(_console, _factory);
        cmd.Execute(new SsmCommands.PutSettings { Name = "/key", Value = "v" }, cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public void RmCommand_forwards_cancellation_token_to_SDK()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        A.CallTo(() => _ssm.DeleteParameterAsync(A<DeleteParameterRequest>._, A<CancellationToken>._))
            .Invokes((DeleteParameterRequest _, CancellationToken ct) => captured = ct);

        var cmd = new SsmCommands.RmCommand(_console, _factory);
        cmd.Execute(new SsmCommands.NameSettings { Name = "/key" }, cts.Token);

        captured.ShouldBe(cts.Token);
    }
}
