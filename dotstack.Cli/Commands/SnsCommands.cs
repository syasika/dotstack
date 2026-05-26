using DotStack.Core.Aws;
using DotStack.Core.Sns;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public static class SnsCommands
{
    public sealed class ArnSettings : EndpointSettings
    {
        [CommandArgument(0, "<topic-arn>")]
        public string TopicArn { get; set; } = "";
    }

    public sealed class NameSettings : EndpointSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";
    }

    public sealed class PublishSettings : EndpointSettings
    {
        [CommandArgument(0, "<topic-arn>")]
        public string TopicArn { get; set; } = "";

        [CommandArgument(1, "<message>")]
        public string Message { get; set; } = "";
    }

    public class LsCommand : Command<EndpointSettings>
    {
        protected override int Execute(
            CommandContext context,
            EndpointSettings settings,
            CancellationToken cancellationToken
        )
        {
            var client = AwsClientFactory.CreateSnsClient(settings.EndpointUrl);
            var topics = SnsOperations
                .ListTopicsAsync(client, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (topics.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey italic]No topics.[/]");
                return 0;
            }
            AnsiConsole.MarkupLine($"[bold white on #0066CC] Topics ({topics.Count}) [/]");
            foreach (var t in topics)
                AnsiConsole.MarkupLine($"  📢 [bold #0044CC]{t.Name}[/]");
            return 0;
        }
    }

    public class CreateCommand : Command<NameSettings>
    {
        protected override int Execute(
            CommandContext context,
            NameSettings settings,
            CancellationToken cancellationToken
        )
        {
            var client = AwsClientFactory.CreateSnsClient(settings.EndpointUrl);
            var t = SnsOperations
                .CreateTopicAsync(client, settings.Name, cancellationToken)
                .GetAwaiter()
                .GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Topic '[bold]{t.Name}[/]' created");
            return 0;
        }
    }

    public class RmCommand : Command<ArnSettings>
    {
        protected override int Execute(
            CommandContext context,
            ArnSettings settings,
            CancellationToken cancellationToken
        )
        {
            var client = AwsClientFactory.CreateSnsClient(settings.EndpointUrl);
            SnsOperations
                .DeleteTopicAsync(client, settings.TopicArn, cancellationToken)
                .GetAwaiter()
                .GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Topic deleted");
            return 0;
        }
    }

    public class PublishCommand : Command<PublishSettings>
    {
        protected override int Execute(
            CommandContext context,
            PublishSettings settings,
            CancellationToken cancellationToken
        )
        {
            var client = AwsClientFactory.CreateSnsClient(settings.EndpointUrl);
            var msgId = SnsOperations
                .PublishMessageAsync(client, settings.TopicArn, settings.Message, cancellationToken)
                .GetAwaiter()
                .GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Message published (id: {msgId})");
            return 0;
        }
    }
}
