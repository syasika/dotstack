using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;

namespace DotStack.Core.Aws;

public static class AwsClientFactory
{
    private static readonly BasicAWSCredentials Credentials = new("miniaws", "miniaws");

    private static AmazonS3Config CreateS3Config(string endpoint) =>
        new()
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            ServiceURL = endpoint,
            ForcePathStyle = true,
            MaxErrorRetry = 1,
        };

    private static AmazonSimpleSystemsManagementConfig CreateSsmConfig(string endpoint) =>
        new()
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            ServiceURL = endpoint,
            MaxErrorRetry = 1,
        };

    private static AmazonSQSConfig CreateSqsConfig(string endpoint) =>
        new()
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            ServiceURL = endpoint,
            MaxErrorRetry = 1,
        };

    private static AmazonSimpleNotificationServiceConfig CreateSnsConfig(string endpoint) =>
        new()
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            ServiceURL = endpoint,
            MaxErrorRetry = 1,
        };

    public static AmazonS3Client CreateS3Client(string endpoint) =>
        new(Credentials, CreateS3Config(endpoint));

    public static AmazonSimpleSystemsManagementClient CreateSsmClient(string endpoint) =>
        new(Credentials, CreateSsmConfig(endpoint));

    public static IAmazonSQS CreateSqsClient(string endpoint) =>
        new AmazonSQSClient(Credentials, CreateSqsConfig(endpoint));

    public static IAmazonSimpleNotificationService CreateSnsClient(string endpoint) =>
        new AmazonSimpleNotificationServiceClient(Credentials, CreateSnsConfig(endpoint));
}
