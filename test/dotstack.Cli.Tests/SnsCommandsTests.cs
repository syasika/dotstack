using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using FakeItEasy;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;
using SnsTopic = Amazon.SimpleNotificationService.Model.Topic;

namespace DotStack.Cli.Tests;

public class SnsCommandsTests
{
    private readonly TestConsole _console;
    private readonly IAwsClientFactory _factory;
    private readonly IAmazonSimpleNotificationService _sns;

    public SnsCommandsTests()
    {
        _console = new TestConsole();
        _sns = A.Fake<IAmazonSimpleNotificationService>();
        _factory = A.Fake<IAwsClientFactory>();
        A.CallTo(() => _factory.CreateSnsClient(A<string>._)).Returns(_sns);
    }

    // ---- LsCommand ----

    [Fact]
    public void LsCommand_no_topics_shows_empty()
    {
        A.CallTo(() => _sns.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .Returns(new ListTopicsResponse { Topics = [] });

        var cmd = new SnsCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("No topics");
    }

    [Fact]
    public void LsCommand_shows_topics()
    {
        A.CallTo(() => _sns.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .Returns(new ListTopicsResponse
            {
                Topics =
                [
                    new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:my-topic" },
                    new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:other-topic" },
                ],
            });

        var cmd = new SnsCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Topics (2)");
        _console.Output.ShouldContain("my-topic");
        _console.Output.ShouldContain("other-topic");
    }

    // ---- CreateCommand ----

    [Fact]
    public void CreateCommand_creates_topic()
    {
        A.CallTo(() => _sns.CreateTopicAsync(
                A<CreateTopicRequest>.That.Matches(r => r.Name == "alerts"),
                A<CancellationToken>._))
            .Returns(new CreateTopicResponse
            {
                TopicArn = "arn:aws:sns:us-east-1:000000000000:alerts",
            });

        var cmd = new SnsCommands.CreateCommand(_console, _factory);
        var result = cmd.Execute(new SnsCommands.NameSettings { Name = "alerts" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("alerts");
        _console.Output.ShouldContain("created");
    }

    // ---- RmCommand ----

    [Fact]
    public void RmCommand_deletes_topic()
    {
        var cmd = new SnsCommands.RmCommand(_console, _factory);
        var result = cmd.Execute(new SnsCommands.ArnSettings
        {
            TopicArn = "arn:aws:sns:us-east-1:000000000000:old",
        });

        result.ShouldBe(0);
        A.CallTo(() => _sns.DeleteTopicAsync(
                A<DeleteTopicRequest>.That.Matches(r =>
                    r.TopicArn == "arn:aws:sns:us-east-1:000000000000:old"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("Topic deleted");
    }

    // ---- PublishCommand ----

    [Fact]
    public void PublishCommand_publishes_message()
    {
        A.CallTo(() => _sns.PublishAsync(
                A<PublishRequest>.That.Matches(r =>
                    r.TopicArn == "arn:aws:sns:us-east-1:000000000000:topic" &&
                    r.Message == "hello"),
                A<CancellationToken>._))
            .Returns(new PublishResponse { MessageId = "pub-456" });

        var cmd = new SnsCommands.PublishCommand(_console, _factory);
        var result = cmd.Execute(new SnsCommands.PublishSettings
        {
            TopicArn = "arn:aws:sns:us-east-1:000000000000:topic",
            Message = "hello",
        });

        result.ShouldBe(0);
        _console.Output.ShouldContain("pub-456");
    }
}
