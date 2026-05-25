using DotStack.Cli.Commands;
using DotStack.Cli.Telemetry;
using DotStack.Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Spectre.Console.Cli;

var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
    .AddSource(ActivitySources.DotStack.Name)
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotstack"));

tracerProviderBuilder.AddProcessor(new SimpleActivityExportProcessor(new StderrActivityExporter()));

if (!string.IsNullOrEmpty(otelEndpoint))
{
    tracerProviderBuilder.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otelEndpoint);
    });
}

using var tracerProvider = tracerProviderBuilder.Build();

var app = new CommandApp();

app.Configure(config =>
{
    config.PropagateExceptions();

    config
        .AddCommand<InitCommand>("init")
        .WithDescription("Initialize dotstack: check/start ministack container");

    config
        .AddCommand<BrowseCommand>("browse")
        .WithDescription("Interactive TUI dashboard for ministack");

    config.AddBranch(
        "s3",
        s3 =>
        {
            s3.SetDescription("Manage S3 resources on ministack");
            s3.AddCommand<S3Commands.LsCommand>("ls");
            s3.AddCommand<S3Commands.MbCommand>("mb");
            s3.AddCommand<S3Commands.RbCommand>("rb");
            s3.AddCommand<S3Commands.CpCommand>("cp");
        }
    );

    config.AddBranch(
        "ssm",
        ssm =>
        {
            ssm.SetDescription("Manage SSM Parameter Store");
            ssm.AddCommand<SsmCommands.LsCommand>("ls");
            ssm.AddCommand<SsmCommands.GetCommand>("get");
            ssm.AddCommand<SsmCommands.PutCommand>("put");
            ssm.AddCommand<SsmCommands.RmCommand>("rm");
        }
    );

    config.AddBranch(
        "sqs",
        sqs =>
        {
            sqs.SetDescription("Manage SQS queues");
            sqs.AddCommand<SqsCommands.LsCommand>("ls");
            sqs.AddCommand<SqsCommands.CreateCommand>("create");
            sqs.AddCommand<SqsCommands.RmCommand>("rm");
            sqs.AddCommand<SqsCommands.SendCommand>("send");
            sqs.AddCommand<SqsCommands.RecvCommand>("recv");
        }
    );

    config.AddBranch(
        "sns",
        sns =>
        {
            sns.SetDescription("Manage SNS topics");
            sns.AddCommand<SnsCommands.LsCommand>("ls");
            sns.AddCommand<SnsCommands.CreateCommand>("create");
            sns.AddCommand<SnsCommands.RmCommand>("rm");
            sns.AddCommand<SnsCommands.PublishCommand>("publish");
        }
    );

    config.AddBranch(
        "container",
        container =>
        {
            container.SetDescription("Manage the ministack container");
            container.AddCommand<ContainerCommands.StatusCommand>("status");
            container.AddCommand<ContainerCommands.StartCommand>("start");
            container.AddCommand<ContainerCommands.StopCommand>("stop");
            container.AddCommand<ContainerCommands.RemoveCommand>("remove");
        }
    );

    config.Settings.ApplicationName = "dotstack";
});

return app.Run(args);
