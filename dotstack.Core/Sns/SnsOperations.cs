using System.Diagnostics;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using DotStack.Core.Aws;
using DotStack.Core.Telemetry;

namespace DotStack.Core.Sns;

public record Topic(string Name, string Arn);

public static class SnsOperations
{
    public static async Task<List<Topic>> ListTopicsAsync(
        AmazonSimpleNotificationServiceClient client, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SNS.ListTopics");
        activity?.SetTag("service", "sns");
        try
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
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SNS");
        }
    }

    public static async Task<Topic> CreateTopicAsync(
        AmazonSimpleNotificationServiceClient client, string name,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SNS.CreateTopic");
        activity?.SetTag("service", "sns");
        activity?.SetTag("topic.name", name);
        try
        {
            var request = new CreateTopicRequest { Name = name };
            var response = await client.CreateTopicAsync(request, ct);
            activity?.SetTag("topic.arn", response.TopicArn);
            return new Topic(name, response.TopicArn);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SNS");
        }
    }

    public static async Task DeleteTopicAsync(
        AmazonSimpleNotificationServiceClient client, string topicArn,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SNS.DeleteTopic");
        activity?.SetTag("service", "sns");
        activity?.SetTag("topic.arn", topicArn);
        try
        {
            var request = new DeleteTopicRequest { TopicArn = topicArn };
            await client.DeleteTopicAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SNS");
        }
    }

    public static async Task<string> PublishMessageAsync(
        AmazonSimpleNotificationServiceClient client,
        string topicArn, string message,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SNS.PublishMessage");
        activity?.SetTag("service", "sns");
        activity?.SetTag("topic.arn", topicArn);
        activity?.SetTag("message.size", message.Length);
        try
        {
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message
            };

            var response = await client.PublishAsync(request, ct);
            activity?.SetTag("message.id", response.MessageId);
            return response.MessageId;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SNS");
        }
    }

    public static string ExtractTopicName(string topicArn)
    {
        var parts = topicArn.Split(':');
        return parts[^1];
    }
}
