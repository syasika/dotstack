using DotStack.Cli.Abstractions;
using DotStack.Core;
using DotStack.Core.Aws;
using DotStack.Core.Sqs;
using Spectre.Console;
using Spectre.Console.Cli;

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
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public LsCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            EndpointSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSqsClient(settings.EndpointUrl);
            var queues = SqsOperations
                .ListQueuesAsync(client, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (queues.Count == 0)
            {
                _console.MarkupLine("[grey italic]No queues.[/]");
                return 0;
            }
            _console.MarkupLine($"[bold white on #0066CC] Queues ({queues.Count}) [/]");
            foreach (var q in queues)
                _console.MarkupLine($"  📦 [bold #0044CC]{q.Name}[/]");
            return 0;
        }
    }

    public class CreateCommand : Command<NameSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public CreateCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            NameSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSqsClient(settings.EndpointUrl);
            var q = SqsOperations
                .CreateQueueAsync(client, settings.Name, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine($"[green bold]✓[/] Queue '[bold]{q.Name}[/]' created");
            return 0;
        }
    }

    public class RmCommand : Command<UrlSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public RmCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            UrlSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSqsClient(settings.EndpointUrl);
            SqsOperations
                .DeleteQueueAsync(client, settings.Url, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine($"[green bold]✓[/] Queue deleted");
            return 0;
        }
    }

    public class SendCommand : Command<SendSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public SendCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            SendSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSqsClient(settings.EndpointUrl);
            var msgId = SqsOperations
                .SendMessageAsync(client, settings.Url, settings.Message, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine($"[green bold]✓[/] Message sent (id: {msgId})");
            return 0;
        }
    }

    public class RecvCommand : Command<RecvSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public RecvCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            RecvSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSqsClient(settings.EndpointUrl);
            var messages = SqsOperations
                .ReceiveMessagesAsync(client, settings.Url, settings.Max, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (messages.Count == 0)
            {
                _console.MarkupLine("[grey italic]No messages.[/]");
                return 0;
            }
            _console.MarkupLine($"[bold white on #0066CC] Messages ({messages.Count}) [/]");
            foreach (var m in messages)
                _console.MarkupLine($"  [grey]{m.Id.EscapeMarkup()}[/]  {m.Body.EscapeMarkup()}");
            return 0;
        }
    }
}
