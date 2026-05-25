using System.Diagnostics;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using DotStack.Core.Aws;
namespace DotStack.Core.Ssm;

public record SsmParameter(
    string Name,
    string Type,
    string Value,
    DateTime LastModified,
    long Version
);

public record SsmPage(SsmParameter[] Parameters, string? NextToken);

public static class SsmOperations
{
    public static Task<List<SsmParameter>> ListAllParametersAsync(
        IAmazonSimpleSystemsManagement client,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SSM.ListAllParameters",
            "ssm",
            async activity =>
            {
                var parameters = new List<SsmParameter>();
                var request = new DescribeParametersRequest();

                DescribeParametersResponse response;
                do
                {
                    response = await client.DescribeParametersAsync(request, ct);
                    if (response.Parameters is { Count: > 0 })
                        parameters.AddRange(response.Parameters.Select(ToParameter));
                    request.NextToken = response.NextToken;
                } while (!string.IsNullOrEmpty(response.NextToken));

                activity?.SetTag("parameter.count", parameters.Count);
                return parameters;
            }
        );

    public static Task<SsmPage> ListParametersAsync(
        IAmazonSimpleSystemsManagement client,
        string? nextToken = null,
        int maxResults = 20,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SSM.ListParameters",
            "ssm",
            async activity =>
            {
                activity?.SetTag("max.results", maxResults);
                activity?.SetTag("has.token", nextToken is not null);

                var request = new DescribeParametersRequest
                {
                    MaxResults = maxResults,
                    NextToken = nextToken,
                };

                var response = await client.DescribeParametersAsync(request, ct);

                activity?.SetTag("parameter.count", response.Parameters?.Count ?? 0);
                activity?.SetTag("has.next", response.NextToken is not null);

                return new SsmPage(
                    response.Parameters?.Select(ToParameter).ToArray() ?? [],
                    response.NextToken
                );
            }
        );

    public static Task<SsmParameter> GetParameterAsync(
        IAmazonSimpleSystemsManagement client,
        string name,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SSM.GetParameter",
            "ssm",
            async activity =>
            {
                activity?.SetTag("parameter.name", name);

                var request = new GetParameterRequest { Name = name, WithDecryption = false };

                var response = await client.GetParameterAsync(request, ct);
                var p = response.Parameter;

                activity?.SetTag("parameter.type", p?.Type);
                activity?.SetTag("parameter.version", p?.Version ?? 0);

                return new SsmParameter(
                    p?.Name ?? string.Empty,
                    p?.Type ?? string.Empty,
                    p?.Value ?? string.Empty,
                    p?.LastModifiedDate ?? DateTime.MinValue,
                    p?.Version ?? 0
                );
            }
        );

    public static Task PutParameterAsync(
        IAmazonSimpleSystemsManagement client,
        string name,
        string value,
        string parameterType = "String",
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SSM.PutParameter",
            "ssm",
            async activity =>
            {
                activity?.SetTag("parameter.name", name);
                activity?.SetTag("parameter.type", parameterType);
                activity?.SetTag("parameter.size", value.Length);

                var request = new PutParameterRequest
                {
                    Name = name,
                    Value = value,
                    Type = parameterType,
                };

                await client.PutParameterAsync(request, ct);
            }
        );

    public static Task DeleteParameterAsync(
        IAmazonSimpleSystemsManagement client,
        string name,
        CancellationToken ct = default
    ) =>
        AwsTracing.TraceAsync(
            "SSM.DeleteParameter",
            "ssm",
            async activity =>
            {
                activity?.SetTag("parameter.name", name);

                var request = new DeleteParameterRequest { Name = name };
                await client.DeleteParameterAsync(request, ct);
            }
        );

    private static SsmParameter ToParameter(
        Amazon.SimpleSystemsManagement.Model.ParameterMetadata p
    ) => new(p.Name, p.Type, string.Empty, p.LastModifiedDate ?? DateTime.MinValue, p.Version ?? 0);
}
