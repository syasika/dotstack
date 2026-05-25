using Xunit;
using Shouldly;
using DotStack.Core.Aws;

namespace DotStack.Core.Tests;

public class AwsClientFactoryTests
{
    [Fact]
    public void CreateS3Client_returns_client()
    {
        var client = AwsClientFactory.CreateS3Client("http://localhost:4566");
        client.ShouldNotBeNull();
        client.Config.ServiceURL.ShouldBe("http://localhost:4566/");
    }

    [Fact]
    public void CreateSsmClient_returns_client()
    {
        var client = AwsClientFactory.CreateSsmClient("http://localhost:4566");
        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateSqsClient_returns_client()
    {
        var client = AwsClientFactory.CreateSqsClient("http://localhost:4566");
        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateSnsClient_returns_client()
    {
        var client = AwsClientFactory.CreateSnsClient("http://localhost:4566");
        client.ShouldNotBeNull();
    }
}
