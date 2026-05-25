using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using DotStack.Core.Aws;
using DotStack.Core.Telemetry;

namespace DotStack.Core.Sqs;

public record Queue(string Name, string Url);

public record Message(string Id, string Body, string ReceiptHandle);

public static class SqsOperations
{
    public static async Task<List<Queue>> ListQueuesAsync(
        AmazonSQSClient client, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.ListQueues");
        activity?.SetTag("service", "sqs");
        try
        {
            var queues = new List<Queue>();
            var request = new ListQueuesRequest();

            ListQueuesResponse response;
            do
            {
                response = await client.ListQueuesAsync(request, ct);
                foreach (var url in response.QueueUrls)
                    queues.Add(new Queue(ExtractQueueName(url), url));

                request.NextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(response.NextToken));

            activity?.SetTag("queue.count", queues.Count);
            return queues;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static async Task<Queue> CreateQueueAsync(
        AmazonSQSClient client, string name, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.CreateQueue");
        activity?.SetTag("service", "sqs");
        activity?.SetTag("queue.name", name);
        try
        {
            var request = new CreateQueueRequest { QueueName = name };
            var response = await client.CreateQueueAsync(request, ct);
            activity?.SetTag("queue.url", response.QueueUrl);
            return new Queue(name, response.QueueUrl);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static async Task DeleteQueueAsync(
        AmazonSQSClient client, string queueUrl, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.DeleteQueue");
        activity?.SetTag("service", "sqs");
        activity?.SetTag("queue.url", queueUrl);
        try
        {
            var request = new DeleteQueueRequest { QueueUrl = queueUrl };
            await client.DeleteQueueAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static async Task<string> SendMessageAsync(
        AmazonSQSClient client, string queueUrl, string body,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.SendMessage");
        activity?.SetTag("service", "sqs");
        activity?.SetTag("queue.url", queueUrl);
        activity?.SetTag("message.size", body.Length);
        try
        {
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = body
            };

            var response = await client.SendMessageAsync(request, ct);
            activity?.SetTag("message.id", response.MessageId);
            return response.MessageId;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static async Task<List<Message>> ReceiveMessagesAsync(
        AmazonSQSClient client, string queueUrl,
        int maxMessages = 10, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.ReceiveMessages");
        activity?.SetTag("service", "sqs");
        activity?.SetTag("queue.url", queueUrl);
        activity?.SetTag("max.messages", maxMessages);
        try
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = maxMessages,
                WaitTimeSeconds = 2
            };

            var response = await client.ReceiveMessageAsync(request, ct);

            var messages = response.Messages
                .Select(m => new Message(m.MessageId, m.Body, m.ReceiptHandle))
                .ToList();

            activity?.SetTag("received.count", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static async Task DeleteMessageAsync(
        AmazonSQSClient client, string queueUrl, string receiptHandle,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SQS.DeleteMessage");
        activity?.SetTag("service", "sqs");
        activity?.SetTag("queue.url", queueUrl);
        try
        {
            var request = new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            };

            await client.DeleteMessageAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SQS");
        }
    }

    public static string ExtractQueueName(string queueUrl)
    {
        var parts = queueUrl.Split('/');
        return parts[^1];
    }
}
