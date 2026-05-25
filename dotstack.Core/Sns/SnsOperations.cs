using System.Diagnostics;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DotStack.Core.Aws;

namespace DotStack.Core.Sns;

public record Topic(string Name, string Arn);

public static class SnsOperations
{
    public static Task<List<Topic>> ListTopicsAsync(
        AmazonSimpleNotificationServiceClient client, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("SNS.ListTopics", "sns", async activity =>
        {
            var topics = new List<Topic>();
            var request = new ListTopicsRequest();

            ListTopicsResponse response;
            do
            {
                response = await client.ListTopicsAsync(request, ct);
                foreach (var t in response.Topics)
                    topics.Add(new Topic(ExtractTopicName(t.TopicArn), t.TopicArn));

                request.NextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(response.NextToken));

            activity?.SetTag("topic.count", topics.Count);
            return topics;
        });

    public static Task<Topic> CreateTopicAsync(
        AmazonSimpleNotificationServiceClient client, string name,
        CancellationToken ct = default) =>
        AwsTracing.TraceAsync("SNS.CreateTopic", "sns", async activity =>
        {
            activity?.SetTag("topic.name", name);

            var request = new CreateTopicRequest { Name = name };
            var response = await client.CreateTopicAsync(request, ct);

            activity?.SetTag("topic.arn", response.TopicArn);
            return new Topic(name, response.TopicArn);
        });

    public static Task DeleteTopicAsync(
        AmazonSimpleNotificationServiceClient client, string topicArn,
        CancellationToken ct = default) =>
        AwsTracing.TraceAsync("SNS.DeleteTopic", "sns", async activity =>
        {
            activity?.SetTag("topic.arn", topicArn);

            var request = new DeleteTopicRequest { TopicArn = topicArn };
            await client.DeleteTopicAsync(request, ct);
        });

    public static Task<string> PublishMessageAsync(
        AmazonSimpleNotificationServiceClient client,
        string topicArn, string message,
        CancellationToken ct = default) =>
        AwsTracing.TraceAsync("SNS.PublishMessage", "sns", async activity =>
        {
            activity?.SetTag("topic.arn", topicArn);
            activity?.SetTag("message.size", message.Length);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message
            };

            var response = await client.PublishAsync(request, ct);
            activity?.SetTag("message.id", response.MessageId);
            return response.MessageId;
        });

    public static string ExtractTopicName(string topicArn)
    {
        var parts = topicArn.Split(':');
        return parts[^1];
    }
}
