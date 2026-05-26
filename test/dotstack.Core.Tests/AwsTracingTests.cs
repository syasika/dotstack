using System.Diagnostics;
using DotStack.Core.Aws;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class AwsTracingTests
{
    private static ActivityListener CreateListener(
        Action<Activity>? onStarted = null,
        Action<Activity>? onStopped = null
    )
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "DotStack.Core",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = onStarted,
            ActivityStopped = onStopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task TraceAsync_T_starts_activity_with_operation_name()
    {
        string? capturedOperationName = null;
        using var _ = CreateListener(activity => capturedOperationName = activity.OperationName);

        await AwsTracing.TraceAsync("TestOp", "test-svc", async _ => "done");

        capturedOperationName.ShouldBe("TestOp");
    }

    [Fact]
    public async Task TraceAsync_T_sets_service_tag()
    {
        Activity? captured = null;
        using var _ = CreateListener(activity => captured = activity);

        await AwsTracing.TraceAsync("Op", "test-svc", async _ => "done");

        captured.ShouldNotBeNull();
        captured.Tags.ShouldContain(kv => kv.Key == "service" && (string)kv.Value! == "test-svc");
    }

    [Fact]
    public async Task TraceAsync_T_returns_inner_call_result()
    {
        using var _ = CreateListener();

        var result = await AwsTracing.TraceAsync("Op", "svc", async _ => 42);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task TraceAsync_T_on_exception_sets_error_status_and_adds_exception_event()
    {
        Activity? captured = null;
        using var _ = CreateListener(
            onStopped: activity => captured = activity
        );

        var innerEx = new InvalidOperationException("boom");
        await Should.ThrowAsync<Exception>(() =>
            AwsTracing.TraceAsync("Op", "svc", async _ => throw innerEx)
        );

        captured.ShouldNotBeNull();
        captured.Status.ShouldBe(ActivityStatusCode.Error);
        captured.StatusDescription.ShouldBe("boom");
        captured.Events.ShouldContain(e => e.Name == "exception");
    }

    [Fact]
    public async Task TraceAsync_T_on_connection_error_rethrows_friendly()
    {
        using var _ = CreateListener();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            AwsTracing.TraceAsync("Op", "s3", async _ =>
                throw new InvalidOperationException("dial tcp: connection refused")
            )
        );

        ex.Message.ShouldContain("cannot reach ministack", Case.Insensitive);
    }

    [Fact]
    public async Task TraceAsync_starts_activity_and_sets_service_tag()
    {
        string? capturedOpName = null;
        Activity? captured = null;
        using var _ = CreateListener(
            activity =>
            {
                capturedOpName = activity.OperationName;
                captured = activity;
            }
        );

        await AwsTracing.TraceAsync("MyOp", "my-svc", async _ => { });

        capturedOpName.ShouldBe("MyOp");
        captured.ShouldNotBeNull();
        captured.Tags.ShouldContain(kv => kv.Key == "service" && (string)kv.Value! == "my-svc");
    }

    [Fact]
    public async Task TraceAsync_on_exception_sets_error_status_and_adds_exception_event()
    {
        Activity? captured = null;
        using var _ = CreateListener(
            onStopped: activity => captured = activity
        );

        await Should.ThrowAsync<Exception>(() =>
            AwsTracing.TraceAsync("Op", "svc", async _ => throw new InvalidOperationException("fail"))
        );

        captured.ShouldNotBeNull();
        captured.Status.ShouldBe(ActivityStatusCode.Error);
        captured.StatusDescription.ShouldBe("fail");
        captured.Events.ShouldContain(e => e.Name == "exception");
    }

    [Fact]
    public async Task TraceAsync_on_connection_error_rethrows_friendly()
    {
        using var _ = CreateListener();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            AwsTracing.TraceAsync("Op", "ec2", async _ =>
                throw new InvalidOperationException("no such host")
            )
        );

        ex.Message.ShouldContain("cannot reach ministack", Case.Insensitive);
    }

    [Fact]
    public async Task TraceAsync_T_disposes_activity_after_call()
    {
        var stoppedCount = 0;
        using var _ = CreateListener(onStopped: _ => stoppedCount++);

        await AwsTracing.TraceAsync("Op", "svc", async _ => "result");

        stoppedCount.ShouldBe(1);
    }

    [Fact]
    public async Task TraceAsync_T_disposes_activity_on_exception()
    {
        var stoppedCount = 0;
        using var _ = CreateListener(onStopped: _ => stoppedCount++);

        await Should.ThrowAsync<Exception>(() =>
            AwsTracing.TraceAsync("Op", "svc", async _ => throw new Exception())
        );

        stoppedCount.ShouldBe(1);
    }

    [Fact]
    public async Task TraceAsync_disposes_activity_after_call()
    {
        var stoppedCount = 0;
        using var _ = CreateListener(onStopped: _ => stoppedCount++);

        await AwsTracing.TraceAsync("Op", "svc", async _ => { });

        stoppedCount.ShouldBe(1);
    }

    [Fact]
    public async Task TraceAsync_disposes_activity_on_exception()
    {
        var stoppedCount = 0;
        using var _ = CreateListener(onStopped: _ => stoppedCount++);

        await Should.ThrowAsync<Exception>(() =>
            AwsTracing.TraceAsync("Op", "svc", async _ => throw new Exception())
        );

        stoppedCount.ShouldBe(1);
    }
}
