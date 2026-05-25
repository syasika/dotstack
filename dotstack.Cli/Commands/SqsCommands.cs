using Spectre.Console;
using Spectre.Console.Cli;
using DotStack.Core.Aws;
using DotStack.Core.Sqs;

namespace DotStack.Cli.Commands;

public static class SqsCommands
{
    public sealed class UrlSettings : EndpointSettings
    {
        [CommandArgument(0, "<url>")]
        public string Url { get; set; } = "";
    }

    public sealed class NameSettings : EndpointSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";
    }

    public sealed class SendSettings : EndpointSettings
    {
        [CommandArgument(0, "<url>")]
        public string Url { get; set; } = "";

        [CommandArgument(1, "<message>")]
        public string Message { get; set; } = "";
    }

    public sealed class RecvSettings : EndpointSettings
    {
        [CommandArgument(0, "<url>")]
        public string Url { get; set; } = "";

        [CommandOption("--max")]
        public int Max { get; set; } = 10;
    }

    public class LsCommand : Command<EndpointSettings>
    {
        protected override int Execute(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSqsClient(settings.EndpointUrl);
            var queues = SqsOperations.ListQueuesAsync(client, cancellationToken).GetAwaiter().GetResult();
            if (queues.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No queues.[/]"); return 0; }
            AnsiConsole.MarkupLine($"[bold white on #0066CC] Queues ({queues.Count}) [/]");
            foreach (var q in queues) AnsiConsole.MarkupLine($"  📦 [bold #0044CC]{q.Name}[/]");
            return 0;
        }
    }

    public class CreateCommand : Command<NameSettings>
    {
        protected override int Execute(CommandContext context, NameSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSqsClient(settings.EndpointUrl);
            var q = SqsOperations.CreateQueueAsync(client, settings.Name, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Queue '[bold]{q.Name}[/]' created");
            return 0;
        }
    }

    public class RmCommand : Command<UrlSettings>
    {
        protected override int Execute(CommandContext context, UrlSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSqsClient(settings.EndpointUrl);
            SqsOperations.DeleteQueueAsync(client, settings.Url, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Queue deleted");
            return 0;
        }
    }

    public class SendCommand : Command<SendSettings>
    {
        protected override int Execute(CommandContext context, SendSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSqsClient(settings.EndpointUrl);
            var msgId = SqsOperations.SendMessageAsync(client, settings.Url, settings.Message, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Message sent (id: {msgId})");
            return 0;
        }
    }

    public class RecvCommand : Command<RecvSettings>
    {
        protected override int Execute(CommandContext context, RecvSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSqsClient(settings.EndpointUrl);
            var messages = SqsOperations.ReceiveMessagesAsync(client, settings.Url, settings.Max, cancellationToken).GetAwaiter().GetResult();
            if (messages.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No messages.[/]"); return 0; }
            AnsiConsole.MarkupLine($"[bold white on #0066CC] Messages ({messages.Count}) [/]");
            foreach (var m in messages) AnsiConsole.MarkupLine($"  [grey][{m.Id}][/] {m.Body}");
            return 0;
        }
    }
}
