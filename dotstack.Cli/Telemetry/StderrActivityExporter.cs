using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;

namespace DotStack.Cli.Telemetry;

internal class StderrActivityExporter : BaseExporter<Activity>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            var entry = new Dictionary<string, object?>
            {
                ["timestamp"] = activity.StartTimeUtc.ToString("O"),
                ["name"] = activity.DisplayName,
                ["durationMs"] = Math.Round(activity.Duration.TotalMilliseconds, 1),
                ["status"] = activity.Status.ToString(),
                ["traceId"] = activity.TraceId.ToString(),
                ["spanId"] = activity.SpanId.ToString(),
                ["parentSpanId"] = activity.ParentSpanId != default
                    ? activity.ParentSpanId.ToString() : null
            };

            // Attributes
            var attrs = new Dictionary<string, object?>();
            foreach (var (key, value) in activity.TagObjects)
            {
                if (value is not null)
                    attrs[key] = value;
            }
            if (attrs.Count > 0)
                entry["attributes"] = attrs;

            // Events (including exceptions)
            if (activity.Events.Any())
            {
                entry["events"] = activity.Events.Select(e => new
                {
                    name = e.Name,
                    timestamp = e.Timestamp.ToString("O"),
                    attributes = e.Tags.ToDictionary(t => t.Key, t => t.Value)
                }).ToList();
            }

            System.Console.Error.WriteLine(
                JsonSerializer.Serialize(entry, JsonOpts));
        }

        return ExportResult.Success;
    }
}
