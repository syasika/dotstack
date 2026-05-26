using Amazon.SQS;
using Amazon.SQS.Model;
using SqsMessage = Amazon.SQS.Model.Message;
using CoreMessage = DotStack.Core.Sqs.Message;
using DotStack.Core.Sqs;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class SqsOperationsTests
{
    [Fact]
    public async Task ListQueuesAsync_returns_queues_with_names()
    {
        var fake = A.Fake<IAmazonSQS>();
        var resp = new ListQueuesResponse
        {
            QueueUrls =
            [
                "http://localhost:4566/000000000000/queue-one",
                "http://localhost:4566/000000000000/queue-two",
            ],
        };
        A.CallTo(() => fake.ListQueuesAsync(A<ListQueuesRequest>._, A<CancellationToken>._))
            .Returns(resp);

        var result = await SqsOperations.ListQueuesAsync(fake);

        result.ShouldBe([
            new Queue("queue-one", "http://localhost:4566/000000000000/queue-one"),
            new Queue("queue-two", "http://localhost:4566/000000000000/queue-two"),
        ]);
    }

    [Fact]
    public async Task ListQueuesAsync_returns_empty_when_QueueUrls_null()
    {
        var fake = A.Fake<IAmazonSQS>();
        A.CallTo(() => fake.ListQueuesAsync(A<ListQueuesRequest>._, A<CancellationToken>._))
            .Returns(new ListQueuesResponse { QueueUrls = null! });

        var result = await SqsOperations.ListQueuesAsync(fake);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListQueuesAsync_paginates_through_all_queues()
    {
        var fake = A.Fake<IAmazonSQS>();

        var page1 = new ListQueuesResponse
        {
            QueueUrls = ["http://localhost:4566/000000000000/q1"],
            NextToken = "token1",
        };
        var page2 = new ListQueuesResponse
        {
            QueueUrls = ["http://localhost:4566/000000000000/q2"],
            NextToken = null,
        };

        A.CallTo(() => fake.ListQueuesAsync(A<ListQueuesRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(page1, page2);

        var result = await SqsOperations.ListQueuesAsync(fake);

        result.ShouldBe([
            new Queue("q1", "http://localhost:4566/000000000000/q1"),
            new Queue("q2", "http://localhost:4566/000000000000/q2"),
        ]);
    }

    [Fact]
    public async Task CreateQueueAsync_returns_queue_with_url()
    {
        var fake = A.Fake<IAmazonSQS>();
        var resp = new CreateQueueResponse
        {
            QueueUrl = "http://localhost:4566/000000000000/my-queue",
        };
        A.CallTo(() =>
                fake.CreateQueueAsync(
                    A<CreateQueueRequest>.That.Matches(r => r.QueueName == "my-queue"),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SqsOperations.CreateQueueAsync(fake, "my-queue");

        result.ShouldBe(new Queue("my-queue", "http://localhost:4566/000000000000/my-queue"));
    }

    [Fact]
    public async Task DeleteQueueAsync_calls_DeleteQueue()
    {
        var fake = A.Fake<IAmazonSQS>();

        await SqsOperations.DeleteQueueAsync(
            fake,
            "http://localhost:4566/000000000000/my-queue"
        );

        A.CallTo(() =>
                fake.DeleteQueueAsync(
                    A<DeleteQueueRequest>.That.Matches(r =>
                        r.QueueUrl == "http://localhost:4566/000000000000/my-queue"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SendMessageAsync_returns_message_id()
    {
        var fake = A.Fake<IAmazonSQS>();
        var resp = new SendMessageResponse { MessageId = "msg-456" };
        A.CallTo(() =>
                fake.SendMessageAsync(
                    A<SendMessageRequest>.That.Matches(r =>
                        r.QueueUrl == "http://queue" && r.MessageBody == "hello"
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SqsOperations.SendMessageAsync(
            fake,
            "http://queue",
            "hello"
        );

        result.ShouldBe("msg-456");
    }

    [Fact]
    public async Task SendMessageAsync_handles_null_MessageId()
    {
        var fake = A.Fake<IAmazonSQS>();
        A.CallTo(() => fake.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
            .Returns(new SendMessageResponse { MessageId = null! });

        var result = await SqsOperations.SendMessageAsync(
            fake,
            "http://queue",
            "hello"
        );

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReceiveMessagesAsync_returns_messages()
    {
        var fake = A.Fake<IAmazonSQS>();
        var resp = new ReceiveMessageResponse
        {
            Messages =
            [
                new SqsMessage
                {
                    MessageId = "m1",
                    Body = "body1",
                    ReceiptHandle = "rh1",
                },
                new SqsMessage
                {
                    MessageId = "m2",
                    Body = "body2",
                    ReceiptHandle = "rh2",
                },
            ],
        };
        A.CallTo(() =>
                fake.ReceiveMessageAsync(
                    A<ReceiveMessageRequest>.That.Matches(r =>
                        r.QueueUrl == "http://queue"
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SqsOperations.ReceiveMessagesAsync(fake, "http://queue");

        result.ShouldBe([
            new CoreMessage("m1", "body1", "rh1"),
            new CoreMessage("m2", "body2", "rh2"),
        ]);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_returns_empty_when_Messages_null()
    {
        var fake = A.Fake<IAmazonSQS>();
        A.CallTo(() =>
                fake.ReceiveMessageAsync(
                    A<ReceiveMessageRequest>._, A<CancellationToken>._
                )
            )
            .Returns(new ReceiveMessageResponse { Messages = null! });

        var result = await SqsOperations.ReceiveMessagesAsync(fake, "http://queue");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReceiveMessagesAsync_uses_maxMessages_and_waitTime()
    {
        var fake = A.Fake<IAmazonSQS>();
        A.CallTo(() =>
                fake.ReceiveMessageAsync(
                    A<ReceiveMessageRequest>.That.Matches(r =>
                        r.QueueUrl == "http://queue"
                        && r.MaxNumberOfMessages == 3
                        && r.WaitTimeSeconds == 2
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(new ReceiveMessageResponse { Messages = [] });

        await SqsOperations.ReceiveMessagesAsync(fake, "http://queue", 3);
    }

    [Fact]
    public async Task DeleteMessageAsync_calls_DeleteMessage()
    {
        var fake = A.Fake<IAmazonSQS>();

        await SqsOperations.DeleteMessageAsync(
            fake,
            "http://queue",
            "receipt-handle-xyz"
        );

        A.CallTo(() =>
                fake.DeleteMessageAsync(
                    A<DeleteMessageRequest>.That.Matches(r =>
                        r.QueueUrl == "http://queue" && r.ReceiptHandle == "receipt-handle-xyz"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void ExtractQueueName_returns_last_part_of_url()
    {
        var name = SqsOperations.ExtractQueueName(
            "http://localhost:4566/000000000000/my-queue"
        );
        name.ShouldBe("my-queue");
    }

    [Fact]
    public void ExtractQueueName_handles_url_with_slashes_in_name()
    {
        var name = SqsOperations.ExtractQueueName(
            "http://localhost:4566/000000000000/a/b/c"
        );
        name.ShouldBe("c");
    }
}
