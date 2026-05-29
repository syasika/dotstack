using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DotStack.Core.Sns;
using FakeItEasy;
using Shouldly;
using Xunit;
using SnsTopic = Amazon.SimpleNotificationService.Model.Topic;
using CoreTopic = DotStack.Core.Sns.Topic;

namespace DotStack.Core.Tests;

public class SnsOperationsTests
{
    [Fact]
    public async Task ListTopicsAsync_returns_topics_with_names()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        var resp = new ListTopicsResponse
        {
            Topics =
            [
                new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:topic-one" },
                new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:topic-two" },
            ],
        };
        A.CallTo(() => fake.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .Returns(resp);

        var result = await SnsOperations.ListTopicsAsync(fake);

        result.ShouldBe([
            new CoreTopic("topic-one", "arn:aws:sns:us-east-1:000000000000:topic-one"),
            new CoreTopic("topic-two", "arn:aws:sns:us-east-1:000000000000:topic-two"),
        ]);
    }

    [Fact]
    public async Task ListTopicsAsync_returns_empty_when_Topics_null()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .Returns(new ListTopicsResponse { Topics = null! });

        var result = await SnsOperations.ListTopicsAsync(fake);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListTopicsAsync_paginates_through_all_topics()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();

        var page1 = new ListTopicsResponse
        {
            Topics =
            [
                new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:t1" },
                new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:t2" },
            ],
            NextToken = "token1",
        };
        var page2 = new ListTopicsResponse
        {
            Topics =
            [
                new SnsTopic { TopicArn = "arn:aws:sns:us-east-1:000000000000:t3" },
            ],
            NextToken = null,
        };

        A.CallTo(() => fake.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .ReturnsNextFromSequence(page1, page2);

        var result = await SnsOperations.ListTopicsAsync(fake);

        result.ShouldBe([
            new CoreTopic("t1", "arn:aws:sns:us-east-1:000000000000:t1"),
            new CoreTopic("t2", "arn:aws:sns:us-east-1:000000000000:t2"),
            new CoreTopic("t3", "arn:aws:sns:us-east-1:000000000000:t3"),
        ]);
    }

    [Fact]
    public async Task CreateTopicAsync_returns_topic_with_arn()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        var resp = new CreateTopicResponse
        {
            TopicArn = "arn:aws:sns:us-east-1:000000000000:my-topic",
        };
        A.CallTo(() =>
                fake.CreateTopicAsync(
                    A<CreateTopicRequest>.That.Matches(r => r.Name == "my-topic"),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SnsOperations.CreateTopicAsync(fake, "my-topic");

        result.ShouldBe(
            new CoreTopic("my-topic", "arn:aws:sns:us-east-1:000000000000:my-topic")
        );
    }

    [Fact]
    public async Task DeleteTopicAsync_calls_DeleteTopic()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();

        await SnsOperations.DeleteTopicAsync(
            fake,
            "arn:aws:sns:us-east-1:000000000000:my-topic"
        );

        A.CallTo(() =>
                fake.DeleteTopicAsync(
                    A<DeleteTopicRequest>.That.Matches(r =>
                        r.TopicArn == "arn:aws:sns:us-east-1:000000000000:my-topic"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishMessageAsync_returns_message_id()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        var resp = new PublishResponse { MessageId = "msg-123" };
        A.CallTo(() =>
                fake.PublishAsync(
                    A<PublishRequest>.That.Matches(r =>
                        r.TopicArn == "arn:topic" && r.Message == "hello"
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SnsOperations.PublishMessageAsync(
            fake,
            "arn:topic",
            "hello"
        );

        result.ShouldBe("msg-123");
    }

    [Fact]
    public async Task PublishMessageAsync_handles_null_MessageId()
    {
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Returns(new PublishResponse { MessageId = null! });

        var result = await SnsOperations.PublishMessageAsync(
            fake,
            "arn:topic",
            "hello"
        );

        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractTopicName_returns_last_part_of_arn()
    {
        var name = SnsOperations.ExtractTopicName(
            "arn:aws:sns:us-east-1:000000000000:my-topic"
        );
        name.ShouldBe("my-topic");
    }

    [Fact]
    public void ExtractTopicName_handles_arn_with_colons_in_name()
    {
        var name = SnsOperations.ExtractTopicName(
            "arn:aws:sns:us-east-1:000000000000:some:thing"
        );
        name.ShouldBe("thing");
    }

    // ---- Cancellation token forwarding ----

    [Fact]
    public async Task ListTopicsAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.ListTopicsAsync(A<ListTopicsRequest>._, A<CancellationToken>._))
            .Invokes((ListTopicsRequest _, CancellationToken ct) => captured = ct)
            .Returns(new ListTopicsResponse { Topics = [] });

        await SnsOperations.ListTopicsAsync(fake, cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task CreateTopicAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.CreateTopicAsync(A<CreateTopicRequest>._, A<CancellationToken>._))
            .Invokes((CreateTopicRequest _, CancellationToken ct) => captured = ct)
            .Returns(new CreateTopicResponse { TopicArn = "arn:aws:sns:us-east-1:000000000000:t" });

        await SnsOperations.CreateTopicAsync(fake, "t", cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task DeleteTopicAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.DeleteTopicAsync(A<DeleteTopicRequest>._, A<CancellationToken>._))
            .Invokes((DeleteTopicRequest _, CancellationToken ct) => captured = ct);

        await SnsOperations.DeleteTopicAsync(fake, "arn:aws:sns:us-east-1:000000000000:t", cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task PublishMessageAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleNotificationService>();
        A.CallTo(() => fake.PublishAsync(A<PublishRequest>._, A<CancellationToken>._))
            .Invokes((PublishRequest _, CancellationToken ct) => captured = ct)
            .Returns(new PublishResponse { MessageId = "m1" });

        await SnsOperations.PublishMessageAsync(fake, "arn:aws:sns:us-east-1:000000000000:t", "msg", cts.Token);

        captured.ShouldBe(cts.Token);
    }
}
