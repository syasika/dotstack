using System.Diagnostics;
using DotStack.Core;
using OpenTelemetry;

namespace DotStack.Cli.Telemetry;

internal class VerboseActivityExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        if (!VerboseConfig.Enabled)
            return ExportResult.Success;

        foreach (var activity in batch)
        {
            var service = activity.GetTagItem("service")?.ToString() ?? "?";
            var ok = activity.Status != ActivityStatusCode.Error;
            var glyph = ok ? "✓" : "✗";
            var statusText = ok ? "OK" : $"ERROR {activity.StatusDescription}";
            var line =
                $"[{service}.{activity.DisplayName}] {glyph}  {activity.Duration.TotalMilliseconds:F0}ms  {statusText}";

            System.Console.Error.WriteLine(line);
        }

        return ExportResult.Success;
    }
}
