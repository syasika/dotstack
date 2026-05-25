using System.Diagnostics;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using DotStack.Core.Aws;
using DotStack.Core.Telemetry;

namespace DotStack.Core.Ssm;

public record SsmParameter(
    string Name,
    string Type,
    string Value,
    DateTime LastModified,
    long Version);

public record SsmPage(
    SsmParameter[] Parameters,
    string? NextToken);

public static class SsmOperations
{
    public static async Task<List<SsmParameter>> ListAllParametersAsync(
        AmazonSimpleSystemsManagementClient client, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SSM.ListAllParameters");
        activity?.SetTag("service", "ssm");
        try
        {
            var parameters = new List<SsmParameter>();
            var request = new DescribeParametersRequest();

            DescribeParametersResponse response;
            do
            {
                response = await client.DescribeParametersAsync(request, ct);
                parameters.AddRange(response.Parameters.Select(ToParameter));
                request.NextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(response.NextToken));

            activity?.SetTag("parameter.count", parameters.Count);
            return parameters;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SSM");
        }
    }

    public static async Task<SsmPage> ListParametersAsync(
        AmazonSimpleSystemsManagementClient client,
        string? nextToken = null,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SSM.ListParameters");
        activity?.SetTag("service", "ssm");
        activity?.SetTag("max.results", maxResults);
        activity?.SetTag("has.token", nextToken is not null);
        try
        {
            var request = new DescribeParametersRequest
            {
                MaxResults = maxResults,
                NextToken = nextToken
            };

            var response = await client.DescribeParametersAsync(request, ct);

            activity?.SetTag("parameter.count", response.Parameters.Count);
            activity?.SetTag("has.next", response.NextToken is not null);

            return new SsmPage(
                response.Parameters.Select(ToParameter).ToArray(),
                response.NextToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SSM");
        }
    }

    public static async Task<SsmParameter> GetParameterAsync(
        AmazonSimpleSystemsManagementClient client, string name,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SSM.GetParameter");
        activity?.SetTag("service", "ssm");
        activity?.SetTag("parameter.name", name);
        try
        {
            var request = new GetParameterRequest
            {
                Name = name,
                WithDecryption = false
            };

            var response = await client.GetParameterAsync(request, ct);
            var p = response.Parameter;

            activity?.SetTag("parameter.type", p.Type);
            activity?.SetTag("parameter.version", p.Version ?? 0);

            return new SsmParameter(
                p.Name,
                p.Type,
                p.Value,
                p.LastModifiedDate ?? DateTime.MinValue,
                p.Version ?? 0);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SSM");
        }
    }

    public static async Task PutParameterAsync(
        AmazonSimpleSystemsManagementClient client,
        string name, string value, string parameterType = "String",
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SSM.PutParameter");
        activity?.SetTag("service", "ssm");
        activity?.SetTag("parameter.name", name);
        activity?.SetTag("parameter.type", parameterType);
        activity?.SetTag("parameter.size", value.Length);
        try
        {
            var request = new PutParameterRequest
            {
                Name = name,
                Value = value,
                Type = parameterType
            };

            await client.PutParameterAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SSM");
        }
    }

    public static async Task DeleteParameterAsync(
        AmazonSimpleSystemsManagementClient client, string name,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("SSM.DeleteParameter");
        activity?.SetTag("service", "ssm");
        activity?.SetTag("parameter.name", name);
        try
        {
            var request = new DeleteParameterRequest { Name = name };
            await client.DeleteParameterAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "SSM");
        }
    }

    private static SsmParameter ToParameter(Amazon.SimpleSystemsManagement.Model.ParameterMetadata p) =>
        new(p.Name, p.Type, string.Empty, p.LastModifiedDate ?? DateTime.MinValue, p.Version ?? 0);
}
