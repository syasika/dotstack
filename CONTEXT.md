# DotStack

A .NET CLI tool to spin up a [ministack](https://hub.docker.com/r/ministackorg/ministack) container (free LocalStack alternative) and manage AWS resources (S3, SSM Parameter Store, SQS, SNS) via a local endpoint.

## Language

**Ministack**:
A Docker container image (`ministackorg/ministack`) that provides a local AWS API endpoint.
_Avoid_: LocalStack, mock AWS, AWS emulator

**Endpoint**:
The base URL where the ministack service is reachable (default `http://localhost:4566`).
_Avoid_: Host, connection string, API URL

**Config**:
Persisted JSON file at `~/.dotstack/config.json` storing container name, image, port, and endpoint URL.
_Avoid_: Settings, preferences, state file

**Container**:
The Docker container running the ministack image, managed via Docker.DotNet.
_Avoid_: Instance, server, runtime

**Service**:
One of the four AWS service wrappers: S3, SSM Parameter Store, SQS, or SNS.
_Avoid_: Module, provider, integration

**Browse**:
An interactive terminal dashboard that displays container status and lets the user navigate S3 buckets, SSM parameters, SQS queues, and SNS topics in a list-driven TUI.
_Avoid_: Dashboard, explorer, TUI (when referring specifically to the browse command)

**Trace**:
A structured record of an AWS API operation (e.g. S3.ListBuckets, SQS.SendMessage) captured as an OpenTelemetry span. Every public method in `*Operations.cs` produces a trace — wrapped by the `AwsTracing` interceptor — with timing, attributes, and error details.
_Avoid_: Log, audit, diagnostic

**AwsTracing**:
A static helper in `DotStack.Core.Aws` that wraps any AWS operation delegate in an Activity span lifecycle and exception mapping. Used by every method in `S3Operations`, `SsmOperations`, `SqsOperations`, and `SnsOperations`. Eliminates ~250 lines of per-method try-catch boilerplate.
_Avoid_: Decorator, middleware, interceptor (when referring to the AwsTracing module specifically)

**Panel**:
A class implementing `IServicePanel` that manages one service's state, render, keyboard input, and sub-navigation in the browse dashboard. One panel per service: `S3Panel`, `SsmPanel`, `SqsPanel`, `SnsPanel`. The `BrowseDashboard` holds a dictionary of panels and delegates to the active one.
_Avoid_: View, widget, component (when referring to a service panel specifically)

**ActivitySource**:
The `DotStack.Core` ActivitySource in `DotStack.Core.Telemetry` — the single source of OpenTelemetry spans in the project. All operations export through this source.
_Avoid_: Tracer, telemetry client

**Verbose Export**:
Human-readable trace summaries written to stderr when `-v`/`--verbose` is set. Each line shows service, operation, status glyph (✓/✗), duration, and error text. Implemented by `VerboseActivityExporter` — a no-op when `VerboseConfig.Enabled` is false.
_Avoid_: Console export, JSONL, stderr logging

**OTLP Export**:
Sends traces to an OpenTelemetry collector or Aspire Dashboard via the OpenTelemetry Protocol. Enabled by setting `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable (e.g. `http://localhost:4317`). When set, takes priority over verbose stderr output. Both can be active simultaneously.
_Avoid_: Aspire integration, telemetry upload

## Example Dialogue

**Dev**: "I ran `dotstack init` and it created the container but the browse dashboard shows 'stopped'."

**Domain Expert**: "The Config stores the container name. Check if the Container was started after creation. Run `dotstack container status` to see the Container state."

**Dev**: "I see the Container status is 'created' not 'running'. Do I need to start it from browse?"

**Domain Expert**: "Press `s` in browse to start the Container, or run `dotstack container start`. The Container must be running for any Service operation to work."

**Dev**: "I uploaded a file with `dotstack s3 cp ./photo.jpg s3://my-bucket/photo.jpg`. Now browse S3 doesn't show it."

**Domain Expert**: "Press `r` to refresh browse. The dashboard caches state — it doesn't auto-poll."
