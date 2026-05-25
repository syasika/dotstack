using Amazon.Runtime;

namespace DotStack.Core.Aws;

public static class AwsExceptionHelper
{
    public static bool IsConnectionError(Exception ex)
    {
        var msg = ex.Message;
        return msg.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("no such host", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("i/o timeout", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("dial tcp", StringComparison.OrdinalIgnoreCase);
    }

    public static Exception ToFriendlyError(Exception ex, string service)
    {
        if (IsConnectionError(ex))
            return new InvalidOperationException(
                "Cannot reach ministack — is the container running?");

        if (ex is AmazonServiceException ase && !string.IsNullOrEmpty(ase.ErrorCode))
            return new InvalidOperationException(
                $"{service} API error: {ase.ErrorCode.ToLowerInvariant()}");

        return ex;
    }
}
