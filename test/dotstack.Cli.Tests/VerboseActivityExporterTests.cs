using System.Diagnostics;
using DotStack.Cli.Telemetry;
using DotStack.Core;
using OpenTelemetry;
using Shouldly;
using Xunit;

namespace DotStack.Cli.Tests;

public class VerboseActivityExporterTests
{
    [Fact]
    public void Export_returns_Success_when_verbose_disabled()
    {
        VerboseConfig.Enabled = false;
        var exporter = new VerboseActivityExporter();
        var activity = new Activity("test");
        var batch = new Batch<Activity>(activity);

        var result = exporter.Export(batch);

        result.ShouldBe(ExportResult.Success);
    }

    [Fact]
    public void Export_returns_Success_when_verbose_enabled_empty_batch()
    {
        VerboseConfig.Enabled = true;
        var exporter = new VerboseActivityExporter();
        var batch = new Batch<Activity>(Array.Empty<Activity>(), 0);

        var result = exporter.Export(batch);

        result.ShouldBe(ExportResult.Success);
    }

    [Fact]
    public void Export_writes_success_line_for_normal_activity()
    {
        VerboseConfig.Enabled = true;
        var exporter = new VerboseActivityExporter();
        var activity = new Activity("list-objects");
        activity.SetTag("service", "s3");
        activity.Start();
        activity.Stop();

        var output = CaptureStderr(() =>
        {
            var batch = new Batch<Activity>(activity);
            exporter.Export(batch);
        });

        output.ShouldContain("[s3.list-objects]");
        output.ShouldContain("OK");
    }

    [Fact]
    public void Export_writes_error_line_for_failed_activity()
    {
        VerboseConfig.Enabled = true;
        var exporter = new VerboseActivityExporter();
        var activity = new Activity("delete-bucket");
        activity.SetTag("service", "s3");
        activity.SetStatus(ActivityStatusCode.Error, "BucketNotEmpty");
        activity.Start();
        activity.Stop();

        var output = CaptureStderr(() =>
        {
            var batch = new Batch<Activity>(activity);
            exporter.Export(batch);
        });

        output.ShouldContain("[s3.delete-bucket]");
        output.ShouldContain("ERROR");
        output.ShouldContain("BucketNotEmpty");
    }

    [Fact]
    public void Export_uses_question_mark_when_service_tag_missing()
    {
        VerboseConfig.Enabled = true;
        var exporter = new VerboseActivityExporter();
        var activity = new Activity("no-service");
        activity.Start();
        activity.Stop();

        var output = CaptureStderr(() =>
        {
            var batch = new Batch<Activity>(activity);
            exporter.Export(batch);
        });

        output.ShouldContain("[?.no-service]");
    }

    [Fact]
    public void Export_handles_multiple_activities()
    {
        VerboseConfig.Enabled = true;
        var exporter = new VerboseActivityExporter();
        var a1 = new Activity("op1");
        a1.SetTag("service", "svc");
        a1.Start();
        a1.Stop();
        var a2 = new Activity("op2");
        a2.SetTag("service", "svc");
        a2.Start();
        a2.Stop();

        var output = CaptureStderr(() =>
        {
            var batch = new Batch<Activity>([a1, a2], 2);
            exporter.Export(batch);
        });

        output.ShouldContain("[svc.op1]");
        output.ShouldContain("[svc.op2]");
    }

    private static string CaptureStderr(Action action)
    {
        var original = System.Console.Error;
        try
        {
            using var writer = new StringWriter();
            System.Console.SetError(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            System.Console.SetError(original);
        }
    }
}
