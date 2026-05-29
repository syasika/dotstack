using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;

namespace DotStack.Cli.Abstractions;

public interface IAwsClientFactory
{
    IAmazonS3 CreateS3Client(string endpointUrl);
    IAmazonSimpleSystemsManagement CreateSsmClient(string endpointUrl);
    IAmazonSQS CreateSqsClient(string endpointUrl);
    IAmazonSimpleNotificationService CreateSnsClient(string endpointUrl);
}
