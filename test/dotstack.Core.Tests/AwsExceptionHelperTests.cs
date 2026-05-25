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
