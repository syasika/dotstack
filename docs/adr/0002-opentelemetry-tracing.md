# OpenTelemetry tracing for AWS operations

`dotstack` had zero observability — all AWS errors were caught, translated to user-friendly messages, and the original exception details were discarded. Debugging failures required reproducing the issue with debugger attached or adding ad-hoc print statements.

The project now uses the **OpenTelemetry .NET SDK** to produce distributed traces for every AWS API call, with two export paths: always-on stderr JSONL and optional OTLP export to an Aspire Dashboard.

**Why not Serilog?** Serilog is logs-only. Traces (`ActivitySource`) were the core requirement — they capture timing, parent-child relationships, and structured attributes per-operation. Serilog's OpenTelemetry sink bridges logs to OTLP but doesn't produce spans. Using the OTel SDK directly gives us both traces and the option to add structured logs later through the same pipeline.

**Why not file logging?** A CLI tool's traces are most useful when they can be inspected live or piped into existing tools. Stderr keeps stdout clean for machine output and works with `2>` redirection, `jq`, or `tail`. OTLP export (when configured) sends to a dedicated dashboard — the best of both channels.

**Why env var for OTLP endpoint?** `OTEL_EXPORTER_OTLP_ENDPOINT` is the OpenTelemetry standard. It's what Aspire Dashboard reads, what docker-compose sets, and what every OTel-aware tool understands. No custom config field needed.

## What was added

- **`dotstack.Core/Telemetry/ActivitySources.cs`** — single `ActivitySource("DotStack.Core")` that all operations use
- **`dotstack.Core/Aws/AwsTracing.cs`** — shared `TraceAsync<T>` / `TraceAsync` interceptor that wraps any delegate in an Activity span + exception mapping, used by every operation method. Replaced ~250 lines of per-method try-catch-activity boilerplate across the four Operations files.
- **Every public method** in `S3Operations`, `SsmOperations`, `SqsOperations`, `SnsOperations` — traced through the `AwsTracing` interceptor with service-specific attributes (bucket name, key, queue URL, topic ARN, counts, etc.)
- **`StderrActivityExporter`** — custom exporter in `dotstack.Cli` that writes each span as a JSONL line to stderr
- **`TracerProvider`** initialized in `Program.cs` with stderr exporter always-on and OTLP exporter when the env var is set

## What wasn't added (and why)

- **No metrics (`Meter`)** — a CLI tool runs one command and exits. Metrics (counters, histograms) need aggregation over time and are meaningful in servers, not short-lived processes.
- **No `ILogger` / structured logs** — the stderr JSONL output carries all the context needed for debugging. If `ILogger` is needed later, `OpenTelemetry.LoggerProvider` integrates through the same OTel pipeline.

## Trade-offs

| Concern | Decision | Rationale |
|---------|----------|-----------|
| SDK selection | OTel SDK over Serilog | Traces are the primary signal. Serilog does logs, not spans. |
| Export fallback | Stderr JSONL always-on | Zero-config debugging. Stdout stays clean. User can `2>traces.jsonl` or `2>&1 \| jq .` |
| OTLP endpoint | `OTEL_EXPORTER_OTLP_ENDPOINT` env var | Standard env var. Works with Aspire Dashboard out of the box. No config field needed. |
| Trace scope | Per AWS operation, not per CLI command | Granular spans show exactly which API call failed, with timing and parameters. |
| ActivitySource location | In `dotstack.Core` | Operations own their spans. Cli only owns the export pipeline. |
| Exception handling | Original exception recorded in span + friendly message to user | Full debugging detail preserved without changing user-facing error UX. |
