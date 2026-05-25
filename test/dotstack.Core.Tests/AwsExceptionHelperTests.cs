using Amazon.Runtime;
using DotStack.Core.Aws;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class AwsExceptionHelperTests
{
    [Fact]
    public void IsConnectionError_connection_refused_returns_true()
    {
        var ex = new InvalidOperationException("connection refused");
        AwsExceptionHelper.IsConnectionError(ex).ShouldBeTrue();
    }

    [Fact]
    public void IsConnectionError_null_message_returns_false()
    {
        var ex = new InvalidOperationException("AccessDenied");
        AwsExceptionHelper.IsConnectionError(ex).ShouldBeFalse();
    }

    [Fact]
    public void IsConnectionError_no_such_host_returns_true()
    {
        var ex = new InvalidOperationException("no such host");
        AwsExceptionHelper.IsConnectionError(ex).ShouldBeTrue();
    }

    [Fact]
    public void IsConnectionError_i_o_timeout_returns_true()
    {
        var ex = new InvalidOperationException("i/o timeout");
        AwsExceptionHelper.IsConnectionError(ex).ShouldBeTrue();
    }

    [Fact]
    public void IsConnectionError_broken_pipe_returns_true()
    {
        var ex = new InvalidOperationException("broken pipe");
        AwsExceptionHelper.IsConnectionError(ex).ShouldBeTrue();
    }

    [Fact]
    public void ToFriendlyError_amazon_service_exception_with_error_code_returns_friendly_api_error()
    {
        var ex = new AmazonServiceException("The security token included in the request is invalid.")
        {
            ErrorCode = "AccessDenied",
        };
        var result = AwsExceptionHelper.ToFriendlyError(ex, "S3");

        result.Message.ShouldContain("s3 API error: accessdenied");
    }

    [Fact]
    public void ToFriendlyError_amazon_service_exception_without_error_code_passes_through()
    {
        var ex = new AmazonServiceException("Unknown error");
        var result = AwsExceptionHelper.ToFriendlyError(ex, "S3");

        result.ShouldBeSameAs(ex);
    }

    [Fact]
    public void ToFriendlyError_connection_error_returns_friendly_message()
    {
        var ex = new InvalidOperationException("dial tcp: connection refused");
        var result = AwsExceptionHelper.ToFriendlyError(ex, "S3");
        result.Message.ShouldContain("cannot reach ministack");
    }

    [Fact]
    public void ToFriendlyError_other_error_passes_through()
    {
        var ex = new InvalidOperationException("Something else");
        var result = AwsExceptionHelper.ToFriendlyError(ex, "S3");
        result.ShouldBeSameAs(ex);
    }
}
