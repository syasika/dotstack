using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using DotStack.Core.Ssm;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

using SsmParameterMetadata = Amazon.SimpleSystemsManagement.Model.ParameterMetadata;

public class SsmOperationsTests
{
    private static readonly DateTime TestDate = new(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListAllParametersAsync_returns_all_parameters()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        var resp = new DescribeParametersResponse
        {
            Parameters =
            [
                new SsmParameterMetadata
                {
                    Name = "/app/db/host",
                    Type = "String",
                    LastModifiedDate = TestDate,
                    Version = 1,
                },
                new SsmParameterMetadata
                {
                    Name = "/app/db/port",
                    Type = "String",
                    LastModifiedDate = TestDate,
                    Version = 2,
                },
            ],
        };
        A.CallTo(() =>
                fake.DescribeParametersAsync(
                    A<DescribeParametersRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SsmOperations.ListAllParametersAsync(fake);

        result.ShouldBe([
            new SsmParameter("/app/db/host", "String", "", TestDate, 1),
            new SsmParameter("/app/db/port", "String", "", TestDate, 2),
        ]);
    }

    [Fact]
    public async Task ListAllParametersAsync_returns_empty_when_Parameters_null()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() =>
                fake.DescribeParametersAsync(
                    A<DescribeParametersRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new DescribeParametersResponse { Parameters = null! });

        var result = await SsmOperations.ListAllParametersAsync(fake);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAllParametersAsync_paginates_through_all()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();

        var page1 = new DescribeParametersResponse
        {
            Parameters =
            [
                new SsmParameterMetadata
                {
                    Name = "/app/p1", Type = "String",
                    LastModifiedDate = TestDate, Version = 1,
                },
            ],
            NextToken = "tok1",
        };
        var page2 = new DescribeParametersResponse
        {
            Parameters =
            [
                new SsmParameterMetadata
                {
                    Name = "/app/p2", Type = "String",
                    LastModifiedDate = TestDate, Version = 2,
                },
            ],
            NextToken = null,
        };

        A.CallTo(() =>
                fake.DescribeParametersAsync(
                    A<DescribeParametersRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsNextFromSequence(page1, page2);

        var result = await SsmOperations.ListAllParametersAsync(fake);

        result.ShouldBe([
            new SsmParameter("/app/p1", "String", "", TestDate, 1),
            new SsmParameter("/app/p2", "String", "", TestDate, 2),
        ]);
    }

    [Fact]
    public async Task ListParametersAsync_returns_page_with_token()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        var resp = new DescribeParametersResponse
        {
            Parameters =
            [
                new SsmParameterMetadata
                {
                    Name = "/app/key", Type = "SecureString",
                    LastModifiedDate = TestDate, Version = 3,
                },
            ],
            NextToken = "next-token",
        };
        A.CallTo(() =>
                fake.DescribeParametersAsync(
                    A<DescribeParametersRequest>.That.Matches(r =>
                        r.MaxResults == 10 && r.NextToken == "prev-token"
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SsmOperations.ListParametersAsync(fake, "prev-token", 10);

        result.Parameters.ShouldBe([
            new SsmParameter("/app/key", "SecureString", "", TestDate, 3),
        ]);
        result.NextToken.ShouldBe("next-token");
    }

    [Fact]
    public async Task ListParametersAsync_returns_empty_when_Parameters_null()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() =>
                fake.DescribeParametersAsync(
                    A<DescribeParametersRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new DescribeParametersResponse { Parameters = null! });

        var result = await SsmOperations.ListParametersAsync(fake);

        result.Parameters.ShouldBeEmpty();
        result.NextToken.ShouldBeNull();
    }

    [Fact]
    public async Task GetParameterAsync_returns_parameter_with_value()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        var resp = new GetParameterResponse
        {
            Parameter = new Amazon.SimpleSystemsManagement.Model.Parameter
            {
                Name = "/app/db/host",
                Type = "String",
                Value = "localhost",
                LastModifiedDate = TestDate,
                Version = 5,
            },
        };
        A.CallTo(() =>
                fake.GetParameterAsync(
                    A<GetParameterRequest>.That.Matches(r =>
                        r.Name == "/app/db/host" && r.WithDecryption == false
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await SsmOperations.GetParameterAsync(fake, "/app/db/host");

        result.ShouldBe(
            new SsmParameter("/app/db/host", "String", "localhost", TestDate, 5)
        );
    }

    [Fact]
    public async Task GetParameterAsync_handles_null_Parameter()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() =>
                fake.GetParameterAsync(
                    A<GetParameterRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new GetParameterResponse { Parameter = null! });

        var result = await SsmOperations.GetParameterAsync(fake, "/app/missing");

        result.ShouldBe(
            new SsmParameter(string.Empty, string.Empty, string.Empty, DateTime.MinValue, 0)
        );
    }

    [Fact]
    public async Task PutParameterAsync_calls_PutParameter()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();

        await SsmOperations.PutParameterAsync(fake, "/app/key", "value123", "SecureString");

        A.CallTo(() =>
                fake.PutParameterAsync(
                    A<PutParameterRequest>.That.Matches(r =>
                        r.Name == "/app/key"
                        && r.Value == "value123"
                        && r.Type == "SecureString"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PutParameterAsync_uses_default_type_String()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();

        await SsmOperations.PutParameterAsync(fake, "/app/key", "val");

        A.CallTo(() =>
                fake.PutParameterAsync(
                    A<PutParameterRequest>.That.Matches(r => r.Type == "String"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteParameterAsync_calls_DeleteParameter()
    {
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();

        await SsmOperations.DeleteParameterAsync(fake, "/app/db/host");

        A.CallTo(() =>
                fake.DeleteParameterAsync(
                    A<DeleteParameterRequest>.That.Matches(r =>
                        r.Name == "/app/db/host"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    // ---- Cancellation token forwarding ----

    [Fact]
    public async Task ListAllParametersAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() => fake.DescribeParametersAsync(A<DescribeParametersRequest>._, A<CancellationToken>._))
            .Invokes((DescribeParametersRequest _, CancellationToken ct) => captured = ct)
            .Returns(new DescribeParametersResponse { Parameters = [] });

        await SsmOperations.ListAllParametersAsync(fake, cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task ListParametersAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() => fake.DescribeParametersAsync(A<DescribeParametersRequest>._, A<CancellationToken>._))
            .Invokes((DescribeParametersRequest _, CancellationToken ct) => captured = ct)
            .Returns(new DescribeParametersResponse { Parameters = [] });

        await SsmOperations.ListParametersAsync(fake, ct: cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task GetParameterAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() => fake.GetParameterAsync(A<GetParameterRequest>._, A<CancellationToken>._))
            .Invokes((GetParameterRequest _, CancellationToken ct) => captured = ct)
            .Returns(new GetParameterResponse
            {
                Parameter = new Amazon.SimpleSystemsManagement.Model.Parameter { Name = "/k", Value = "v" },
            });

        await SsmOperations.GetParameterAsync(fake, "/k", cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task PutParameterAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() => fake.PutParameterAsync(A<PutParameterRequest>._, A<CancellationToken>._))
            .Invokes((PutParameterRequest _, CancellationToken ct) => captured = ct);

        await SsmOperations.PutParameterAsync(fake, "/k", "v", ct: cts.Token);

        captured.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task DeleteParameterAsync_forwards_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var fake = A.Fake<IAmazonSimpleSystemsManagement>();
        A.CallTo(() => fake.DeleteParameterAsync(A<DeleteParameterRequest>._, A<CancellationToken>._))
            .Invokes((DeleteParameterRequest _, CancellationToken ct) => captured = ct);

        await SsmOperations.DeleteParameterAsync(fake, "/k", cts.Token);

        captured.ShouldBe(cts.Token);
    }
}
