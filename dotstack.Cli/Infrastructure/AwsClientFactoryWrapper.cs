using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using DotStack.Cli.Abstractions;
using DotStack.Core.Aws;

namespace DotStack.Cli.Infrastructure;

public sealed class AwsClientFactoryWrapper : IAwsClientFactory
{
    public IAmazonS3 CreateS3Client(string endpointUrl) =>
        AwsClientFactory.CreateS3Client(endpointUrl);

    public IAmazonSimpleSystemsManagement CreateSsmClient(string endpointUrl) =>
        AwsClientFactory.CreateSsmClient(endpointUrl);

    public IAmazonSQS CreateSqsClient(string endpointUrl) =>
        AwsClientFactory.CreateSqsClient(endpointUrl);

    public IAmazonSimpleNotificationService CreateSnsClient(string endpointUrl) =>
        AwsClientFactory.CreateSnsClient(endpointUrl);
}
