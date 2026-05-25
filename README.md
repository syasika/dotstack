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

Pagination shows `[more →]` when more than 20 parameters exist.

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
| `q` | Quit dashboard |
| `s` | Start container (if stopped) |
| `x` | Stop container (if running) |

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

## Telemetry / OpenTelemetry Tracing

Every AWS API operation (S3 bucket list, SQS send, SSM get, etc.) produces an **OpenTelemetry trace** — a structured span with timing, parameters, and error details. No configuration needed.

### Always-on: stderr JSONL

All traces are written as JSON lines to stderr. Stdout stays clean for command output.

```bash
# Capture traces to a file
dotstack s3 ls 2>traces.jsonl

# Live view with jq
dotstack s3 cp ./photo.jpg s3://my-bucket/ 2>&1 | jq .
```

Example trace output on error:

```json
{
  "timestamp": "2026-05-25T20:38:58.4097962Z",
  "name": "S3.ListObjects",
  "durationMs": 178.5,
  "status": "Error",
  "traceId": "96708b1626954226...",
  "spanId": "a3c5f2763a24d320",
  "attributes": {"bucket": "nonexistent-bucket"},
  "events": [{
    "name": "exception",
    "attributes": {
      "exception.type": "Amazon.S3.Model.NoSuchBucketException",
      "exception.message": "The specified bucket does not exist",
      "exception.stacktrace": "..."
    }
  }]
}
```

### Optional: Aspire Dashboard

Set `OTEL_EXPORTER_OTLP_ENDPOINT` to send traces to an OpenTelemetry Collector or Aspire Dashboard:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotstack s3 ls
```

The Aspire Dashboard shows the full trace waterfall — which operations ran, how long they took, and where errors occurred.

### Operations traced

| Service | Operations |
|---------|------------|
| S3 | ListBuckets, ListObjects, CreateBucket, DeleteBucket, EmptyBucket, UploadFile, DownloadFile, DeleteObject |
| SSM | ListAllParameters, ListParameters, GetParameter, PutParameter, DeleteParameter |
| SQS | ListQueues, CreateQueue, DeleteQueue, SendMessage, ReceiveMessages, DeleteMessage |
| SNS | ListTopics, CreateTopic, DeleteTopic, PublishMessage |

Error spans include the original exception type, message, and full stack trace — visible in the trace dashboard or the stderr JSONL output. The user-facing CLI output still shows the same friendly error messages (e.g. "S3 API error: nosuchbucket").

See [docs/adr/0002-opentelemetry-tracing.md](docs/adr/0002-opentelemetry-tracing.md) for the design decisions.

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
| `dotstack.Cli` | CLI entry point (`Program.cs`), Spectre.Console.Cli command tree |
| `dotstack.Tui` | Live dashboard (`BrowseDashboard.cs`), async refresh loop, keyboard navigation |
| `dotstack.Core` | `Config` persistence, `AwsClientFactory`, typed operation wrappers per service |

AWS clients use dummy credentials (`miniaws`/`miniaws`) and point at the local ministack endpoint. All AWS errors are translated to friendly messages (connection refused → "is the container running?").

## License

MIT
