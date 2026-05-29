using Amazon.SQS;
using Amazon.SQS.Model;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using FakeItEasy;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace DotStack.Cli.Tests;

public class SqsCommandsTests
{
    private readonly TestConsole _console;
    private readonly IAwsClientFactory _factory;
    private readonly IAmazonSQS _sqs;

    public SqsCommandsTests()
    {
        _console = new TestConsole();
        _sqs = A.Fake<IAmazonSQS>();
        _factory = A.Fake<IAwsClientFactory>();
        A.CallTo(() => _factory.CreateSqsClient(A<string>._)).Returns(_sqs);
    }

    // ---- LsCommand ----

    [Fact]
    public void LsCommand_no_queues_shows_empty()
    {
        A.CallTo(() => _sqs.ListQueuesAsync(A<ListQueuesRequest>._, A<CancellationToken>._))
            .Returns(new ListQueuesResponse { QueueUrls = [] });

        var cmd = new SqsCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("No queues");
    }

    [Fact]
    public void LsCommand_shows_queues()
    {
        A.CallTo(() => _sqs.ListQueuesAsync(A<ListQueuesRequest>._, A<CancellationToken>._))
            .Returns(new ListQueuesResponse
            {
                QueueUrls =
                [
                    "http://localhost:4566/000000000000/my-queue",
                    "http://localhost:4566/000000000000/other-queue",
                ],
            });

        var cmd = new SqsCommands.LsCommand(_console, _factory);
        var result = cmd.Execute(new EndpointSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Queues (2)");
        _console.Output.ShouldContain("my-queue");
        _console.Output.ShouldContain("other-queue");
    }

    // ---- CreateCommand ----

    [Fact]
    public void CreateCommand_creates_queue()
    {
        A.CallTo(() => _sqs.CreateQueueAsync(
                A<CreateQueueRequest>.That.Matches(r => r.QueueName == "test-queue"),
                A<CancellationToken>._))
            .Returns(new CreateQueueResponse { QueueUrl = "http://localhost:4566/000000000000/test-queue" });

        var cmd = new SqsCommands.CreateCommand(_console, _factory);
        var result = cmd.Execute(new SqsCommands.NameSettings { Name = "test-queue" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("test-queue");
        _console.Output.ShouldContain("created");
    }

    // ---- RmCommand ----

    [Fact]
    public void RmCommand_deletes_queue()
    {
        var cmd = new SqsCommands.RmCommand(_console, _factory);
        var result = cmd.Execute(new SqsCommands.UrlSettings
        {
            Url = "http://localhost:4566/000000000000/to-delete",
        });

        result.ShouldBe(0);
        A.CallTo(() => _sqs.DeleteQueueAsync(
                A<DeleteQueueRequest>.That.Matches(r =>
                    r.QueueUrl == "http://localhost:4566/000000000000/to-delete"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("Queue deleted");
    }

    // ---- SendCommand ----

    [Fact]
    public void SendCommand_sends_message()
    {
        A.CallTo(() => _sqs.SendMessageAsync(
                A<SendMessageRequest>.That.Matches(r =>
                    r.QueueUrl == "http://localhost:4566/000000000000/q" &&
                    r.MessageBody == "hello"),
                A<CancellationToken>._))
            .Returns(new SendMessageResponse { MessageId = "msg-123" });

        var cmd = new SqsCommands.SendCommand(_console, _factory);
        var result = cmd.Execute(new SqsCommands.SendSettings
        {
            Url = "http://localhost:4566/000000000000/q",
            Message = "hello",
        });

        result.ShouldBe(0);
        _console.Output.ShouldContain("msg-123");
    }

    // ---- RecvCommand ----

    [Fact]
    public void RecvCommand_shows_messages()
    {
        A.CallTo(() => _sqs.ReceiveMessageAsync(
                A<ReceiveMessageRequest>.That.Matches(r =>
                    r.QueueUrl == "http://localhost:4566/000000000000/q" &&
                    r.MaxNumberOfMessages == 10),
                A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Amazon.SQS.Model.Message { MessageId = "1", Body = "body-one" },
                    new Amazon.SQS.Model.Message { MessageId = "2", Body = "body-two" },
                ],
            });

        var cmd = new SqsCommands.RecvCommand(_console, _factory);
        var result = cmd.Execute(new SqsCommands.RecvSettings
        {
            Url = "http://localhost:4566/000000000000/q",
        });

        result.ShouldBe(0);
        _console.Output.ShouldContain("Messages (2)");
        _console.Output.ShouldContain("body-one");
        _console.Output.ShouldContain("body-two");
    }

    [Fact]
    public void RecvCommand_no_messages_shows_empty()
    {
        A.CallTo(() => _sqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
            .Returns(new ReceiveMessageResponse { Messages = [] });

        var cmd = new SqsCommands.RecvCommand(_console, _factory);
        var result = cmd.Execute(new SqsCommands.RecvSettings
        {
            Url = "http://localhost:4566/000000000000/q",
        });

        result.ShouldBe(0);
        _console.Output.ShouldContain("No messages");
    }
}
