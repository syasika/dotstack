using System.Diagnostics;

namespace DotStack.Core.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource DotStack = new("DotStack.Core", "1.0.0");
}
