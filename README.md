# dotstack

A .NET CLI tool + TUI to spin up a [ministack](https://hub.docker.com/r/ministackorg/ministack) container (free LocalStack alternative) and manage AWS resources — S3, SSM Parameter Store, SQS, SNS — via a local endpoint.

Port of [miniaws](https://github.com/syasika/miniaws) (Go) to .NET with Spectre.Console, packaged as a single [`dotstack` CLI](#commands).

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Docker Engine (for container lifecycle commands)

## Install

Run from source with the .NET SDK, or build a self-contained executable:

```bash
# Build the CLI
dotnet build

# Run directly
dotnet run --project dotstack.Cli -- <command>

# Build a single-file executable
dotnet publish dotstack.Cli -c Release -r linux-x64 --self-contained -o ./publish
./publish/dotstack <command>
```

## Quick Start

```bash
# Start a ministack container (interactive — prompts for name & image)
dotstack init

# S3 — list buckets, create, upload, download
dotstack s3 ls
dotstack s3 mb my-bucket
dotstack s3 cp ./photo.jpg s3://my-bucket/photo.jpg

# SSM Parameter Store — list, get, put, delete
dotstack ssm ls
dotstack ssm put /config/db-url "localhost"
dotstack ssm get /config/db-url

# SQS — list queues, create, send, receive
dotstack sqs ls
dotstack sqs create my-queue
dotstack sqs send http://localhost:4566/queue/my-queue "hello"
dotstack sqs recv http://localhost:4566/queue/my-queue

# SNS — list topics, create, publish
dotstack sns ls
dotstack sns create my-topic
dotstack sns publish arn:aws:sns:us-east-1:000000000000:my-topic "hello"

# Container lifecycle
dotstack container status
dotstack container stop
dotstack container remove

# Interactive dashboard
dotstack browse
```

## Commands

### `dotstack init`

Ensure the ministack container exists and is running. If no config found, prompts for container name, image name, and port, then pulls the image and starts the container.

No arguments. Returns 0 if container is already running.

### `dotstack browse`

Launch the interactive TUI dashboard (Spectre.Console live panel). See [Browse TUI](#browse-tui) below.

### `dotstack s3`

Manage S3 resources.

| Subcommand | Arguments | Description |
|---|---|---|
| `s3 ls [bucket]` | `bucket` — optional; if omitted shows all buckets, if given shows objects inside it (supports `s3://` prefix) | List buckets or objects |
| `s3 mb <bucket>` | `bucket` — bucket name (supports `s3://` prefix) | Create a bucket |
| `s3 rb <bucket>` | `bucket` — bucket name (supports `s3://` prefix)<br>`-f, --force` — empty bucket before deleting | Remove (and optionally force-empty) a bucket |
| `s3 cp <source> <dest>` | `source`, `dest` — exactly one must be an `s3://path` and the other a local filesystem path | Upload or download a file |

### `dotstack ssm`

Manage SSM Parameter Store.

| Subcommand | Arguments | Description |
|---|---|---|
| `ssm ls` | — | List all parameters |
| `ssm get <name>` | `name` — parameter path | Get parameter name, type, value, version |
| `ssm put <name> <value>` | `name` — parameter path<br>`value` — parameter value<br>`--type` — parameter type (default: `String`) | Create or update a parameter |
| `ssm rm <name>` | `name` — parameter path | Delete a parameter |

### `dotstack sqs`

Manage SQS queues.

| Subcommand | Arguments | Description |
|---|---|---|
| `sqs ls` | — | List all queues (name + URL) |
| `sqs create <name>` | `name` — queue name | Create a queue |
| `sqs rm <url>` | `url` — queue URL | Delete a queue |
| `sqs send <url> <message>` | `url` — queue URL<br>`message` — message body | Send a message to a queue |
| `sqs recv <url>` | `url` — queue URL<br>`--max` — max messages to receive (default: 10) | Receive messages from a queue |

### `dotstack sns`

Manage SNS topics.

| Subcommand | Arguments | Description |
|---|---|---|
| `sns ls` | — | List all topics (name + ARN) |
| `sns create <name>` | `name` — topic name | Create a topic |
| `sns rm <topic-arn>` | `topic-arn` — topic ARN | Delete a topic |
| `sns publish <topic-arn> <message>` | `topic-arn` — topic ARN<br>`message` — message body | Publish a message to a topic |

### `dotstack container`

Manage the ministack Docker container.

| Subcommand | Arguments | Description |
|---|---|---|
| `container status` | — | Show container state (name, image, status, started time if running) |
| `container start` | — | Start the container |
| `container stop` | — | Stop the container |
| `container remove` | `-f, --force` — force remove running container | Remove the container and delete config |

### Global Flags

| Flag | Default | Description |
|---|---|---|
| `-e, --endpoint-url` | `http://localhost:4566` | Ministack API endpoint |
| `-v, --verbose` | `false` | Print human-readable trace summaries for each AWS operation |

## Browse TUI

Launch with `dotstack browse`. An interactive live dashboard showing container status and resource browser.

```
☁  dotstack — ministack dashboard

  Container
    ● running  (ministack)

  [1] S3   [2] SSM   [3] SQS   [4] SNS

  S3 Buckets
   ▸ my-bucket
     assets

  ↑/↓ nav · enter browse · del delete · s start · q quit
```

The dashboard (`BrowseDashboard`) is a thin orchestrator. Each AWS service has its own panel (`IServicePanel`) managing its state, render, keyboard handling, and sub-navigation independently.

### Tabs

Press number keys to switch service:

| Key | Service |
|---|---|
| `1` | S3 |
| `2` | SSM Parameter Store |
| `3` | SQS |
| `4` | SNS |

### S3 View

| State | Controls |
|---|---|
| **Bucket list** | `↑`/`↓` or `k`/`j` navigate, `enter` browse objects inside bucket, `r` refresh |
| **Object list** | `↑`/`↓` or `k`/`j` navigate, `del`/`backspace` delete selected object (with confirmation), `esc` go back, `r` refresh |

### SSM View

| Controls |
|---|
| `↑`/`↓` or `k`/`j` navigate, `enter` show parameter value in status bar, `del`/`backspace` delete parameter (with confirmation), `r` refresh |

### SQS View

| State | Controls |
|---|---|
| **Queue list** | `↑`/`↓` or `k`/`j` navigate, `enter` view messages, `del`/`backspace` delete queue (with confirmation), `r` refresh |
| **Message list** | `↑`/`↓` or `k`/`j` navigate, `del`/`backspace` delete message (with confirmation), `esc` go back, `r` refresh |

### SNS View

| Controls |
|---|
| `↑`/`↓` or `k`/`j` navigate, `del`/`backspace` delete topic (with confirmation), `r` refresh |

### Global Keys

| Key | Action |
|---|---|
| `1`‑`4` | Switch service panel |
| `q` / `esc` | Quit dashboard |
| `r` | Refresh current panel |

### Destructive Actions

All delete operations prompt for confirmation (`y`/`N`) before executing.

## Configuration

Config is stored as JSON at `~/.dotstack/config.json`:

```json
{
  "containerName": "ministack",
  "imageName": "ministackorg/ministack",
  "port": "4566",
  "endpointUrl": "http://localhost:4566"
}
```

Created by `init`, removed by `container remove`. Auto-loaded by all commands.

## Verbose Mode

Pass `-v` or `--verbose` to print a human-readable trace line for every AWS operation:

```bash
dotstack s3 ls -v
[S3.ListBuckets] ✓  42ms  OK
```

On error the trace shows the failure:

```bash
dotstack s3 rb nonexistent-bucket -v
[S3.DeleteBucket] ✗  178ms  ERROR The specified bucket does not exist
```

Verbose output goes to stderr, so stdout stays clean for scripting:

```bash
dotstack s3 ls -v 2>&1 | grep "✓"       # filter successful operations only
dotstack s3 cp ./f s3://b/ -v 2>traces   # capture traces to file
```

Operations traced:

| Service | Operations |
|---------|------------|
| S3 | ListBuckets, ListObjects, CreateBucket, DeleteBucket, EmptyBucket, UploadFile, DownloadFile, DeleteObject |
| SSM | ListAllParameters, ListParameters, GetParameter, PutParameter, DeleteParameter |
| SQS | ListQueues, CreateQueue, DeleteQueue, SendMessage, ReceiveMessages, DeleteMessage |
| SNS | ListTopics, CreateTopic, DeleteTopic, PublishMessage |

### OTLP Export (Aspire Dashboard)

Set `OTEL_EXPORTER_OTLP_ENDPOINT` to send full structured traces to an OpenTelemetry Collector or Aspire Dashboard:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotstack s3 ls
```

The Aspire Dashboard shows the full trace waterfall — timing, attributes, and error details with exception stack traces.

When OTLP is configured, the verbose flag still works alongside it.

See [docs/adr/0002-opentelemetry-tracing.md](docs/adr/0002-opentelemetry-tracing.md) for the design decisions.

## Telemetry / OpenTelemetry Tracing

Every AWS API operation produces an **OpenTelemetry trace** — a structured span with timing, parameters, and error details. The `AwsTracing` interceptor in `dotstack.Core` wraps all operations.

Error spans include the original exception type, message, and full stack trace — visible in the OTLP dashboard or via `-v` stderr output. User-facing CLI output still shows friendly error messages (e.g. "S3 API error: nosuchbucket").

## Architecture

Three-project solution enforcing layered dependencies:

```
dotstack.Cli   ← command parsing, user-facing output (Spectre.Console.Cli)
dotstack.Tui   ← interactive dashboard (Spectre.Console live panel)
dotstack.Core  ← AWS SDK wrappers, config, Docker client
```

Dependency direction: `Cli → Core`, `Cli → Tui`, `Tui → Core`. No cycles. See [docs/adr/0001-multi-project-architecture.md](docs/adr/0001-multi-project-architecture.md).

Projects:

| Project | Role |
|---|---|
| `dotstack.Cli` | CLI entry point (`Program.cs`), Spectre.Console.Cli command tree, OpenTelemetry tracer setup |
| `dotstack.Tui` | Live dashboard orchestrated by `BrowseDashboard`, per-service panels (`S3Panel`, `SsmPanel`, `SqsPanel`, `SnsPanel`) implementing `IServicePanel` |
| `dotstack.Core` | `Config` persistence, `AwsClientFactory`, typed operation wrappers per service, `AwsTracing` interceptor for consistent trace + error handling |

AWS clients use dummy credentials (`miniaws`/`miniaws`) and point at the local ministack endpoint. AWS errors are translated to friendly messages (connection refused → "is the container running?").

### Tracing interceptor

Every AWS operation (S3.ListBuckets, SQS.SendMessage, etc.) is wrapped by `AwsTracing.TraceAsync` in `dotstack.Core/Aws/AwsTracing.cs`. This single module handles:
- Activity span lifecycle (start/stop via `ActivitySources.DotStack`)
- Service-scoped tags (`service`, bucket, queue URL, etc.)
- Error status recording + exception capture
- Translation to user-friendly errors via `AwsExceptionHelper`

Operations files (`S3Operations`, `SsmOperations`, etc.) are pure AWS method calls — no try-catch or tracing logic inlined.

### Dashboard panels

The TUI dashboard (`BrowseDashboard`) is a thin orchestrator. Each AWS service has its own panel class implementing `IServicePanel`:

| Panel | Path |
|---|---|
| `S3Panel` | `dotstack.Tui/S3Panel.cs` |
| `SsmPanel` | `dotstack.Tui/SsmPanel.cs` |
| `SqsPanel` | `dotstack.Tui/SqsPanel.cs` |
| `SnsPanel` | `dotstack.Tui/SnsPanel.cs` |

The dashboard routes keyboard input and switches panels on tab press. Each panel manages its own state, cursor, render, refresh, and sub-navigation (bucket → objects, queue → messages). Adding a new service requires one new panel class + one registration in `BrowseDashboard`.

## License

MIT
