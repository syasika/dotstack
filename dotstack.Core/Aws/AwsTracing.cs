using System.Diagnostics;
using DotStack.Core.Telemetry;

namespace DotStack.Core.Aws;

public static class AwsTracing
{
    public static async Task<T> TraceAsync<T>(
        string operation,
        string service,
        Func<Activity?, Task<T>> call
    )
    {
        using var activity = ActivitySources.DotStack.StartActivity(operation);
        activity?.SetTag("service", service);
        try
        {
            return await call(activity);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, service);
        }
    }

    public static async Task TraceAsync(
        string operation,
        string service,
        Func<Activity?, Task> call
    )
    {
        using var activity = ActivitySources.DotStack.StartActivity(operation);
        activity?.SetTag("service", service);
        try
        {
            await call(activity);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, service);
        }
    }
}
