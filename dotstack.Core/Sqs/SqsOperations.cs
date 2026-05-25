using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using DotStack.Core.Aws;

namespace DotStack.Core.Sqs;

public record Queue(string Name, string Url);

public record Message(string Id, string Body, string ReceiptHandle);

public static class SqsOperations
{
    public static Task<List<Queue>> ListQueuesAsync(
        AmazonSQSClient client,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.ListQueues",
            "sqs",
            async activity =>
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
                } while (!string.IsNullOrEmpty(response.NextToken));

                activity?.SetTag("queue.count", queues.Count);
                return queues;
            }
        );

    public static Task<Queue> CreateQueueAsync(
        AmazonSQSClient client,
        string name,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.CreateQueue",
            "sqs",
            async activity =>
            {
                activity?.SetTag("queue.name", name);

                var request = new CreateQueueRequest { QueueName = name };
                var response = await client.CreateQueueAsync(request, ct);

                activity?.SetTag("queue.url", response.QueueUrl);
                return new Queue(name, response.QueueUrl);
            }
        );

    public static Task DeleteQueueAsync(
        AmazonSQSClient client,
        string queueUrl,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.DeleteQueue",
            "sqs",
            async activity =>
            {
                activity?.SetTag("queue.url", queueUrl);

                var request = new DeleteQueueRequest { QueueUrl = queueUrl };
                await client.DeleteQueueAsync(request, ct);
            }
        );

    public static Task<string> SendMessageAsync(
        AmazonSQSClient client,
        string queueUrl,
        string body,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.SendMessage",
            "sqs",
            async activity =>
            {
                activity?.SetTag("queue.url", queueUrl);
                activity?.SetTag("message.size", body.Length);

                var request = new SendMessageRequest { QueueUrl = queueUrl, MessageBody = body };

                var response = await client.SendMessageAsync(request, ct);
                activity?.SetTag("message.id", response.MessageId);
                return response.MessageId;
            }
        );

    public static Task<List<Message>> ReceiveMessagesAsync(
        AmazonSQSClient client,
        string queueUrl,
        int maxMessages = 10,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.ReceiveMessages",
            "sqs",
            async activity =>
            {
                activity?.SetTag("queue.url", queueUrl);
                activity?.SetTag("max.messages", maxMessages);

                var request = new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = maxMessages,
                    WaitTimeSeconds = 2,
                };

                var response = await client.ReceiveMessageAsync(request, ct);

                var messages = response
                    .Messages.Select(m => new Message(m.MessageId, m.Body, m.ReceiptHandle))
                    .ToList();

                activity?.SetTag("received.count", messages.Count);
                return messages;
            }
        );

    public static Task DeleteMessageAsync(
        AmazonSQSClient client,
        string queueUrl,
        string receiptHandle,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SQS.DeleteMessage",
            "sqs",
            async activity =>
            {
                activity?.SetTag("queue.url", queueUrl);

                var request = new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = receiptHandle,
                };

                await client.DeleteMessageAsync(request, ct);
            }
        );

    public static string ExtractQueueName(string queueUrl)
    {
        var parts = queueUrl.Split('/');
        return parts[^1];
    }
}
