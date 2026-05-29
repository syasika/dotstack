using DotStack.Cli.Abstractions;
using DotStack.Core;
using DotStack.Core.Aws;
using DotStack.Core.Ssm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public static class SsmCommands
{
    public sealed class NameSettings : EndpointSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";
    }

    public sealed class PutSettings : EndpointSettings
    {
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";

        [CommandArgument(1, "<value>")]
        public string Value { get; set; } = "";

        [CommandOption("--type")]
        public string? Type { get; set; }
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
            var client = _clientFactory.CreateSsmClient(settings.EndpointUrl);
            var parameters = SsmOperations
                .ListAllParametersAsync(client, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (parameters.Count == 0)
            {
                _console.MarkupLine("[grey italic]No parameters.[/]");
                return 0;
            }
            _console.MarkupLine($"[bold white on #0066CC] Parameters ({parameters.Count}) [/]");
            foreach (var p in parameters)
                _console.MarkupLine(
                    $"  [bold #0044CC]{p.Name}[/]  [grey]({p.Type}, v{p.Version})[/]"
                );
            return 0;
        }
    }

    public class GetCommand : Command<NameSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public GetCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
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
            var client = _clientFactory.CreateSsmClient(settings.EndpointUrl);
            var p = SsmOperations
                .GetParameterAsync(client, settings.Name, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine($"[bold]Name:[/]  {p.Name}");
            _console.MarkupLine($"[bold]Type:[/]  {p.Type}");
            _console.MarkupLine($"[bold]Value:[/] {p.Value}");
            _console.MarkupLine($"[bold]Version:[/]  v{p.Version}");
            return 0;
        }
    }

    public class PutCommand : Command<PutSettings>
    {
        private readonly IAnsiConsole _console;
        private readonly IAwsClientFactory _clientFactory;

        public PutCommand(IAnsiConsole console, IAwsClientFactory clientFactory)
        {
            _console = console;
            _clientFactory = clientFactory;
        }

        protected override int Execute(
            CommandContext context,
            PutSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSsmClient(settings.EndpointUrl);
            SsmOperations
                .PutParameterAsync(
                    client,
                    settings.Name,
                    settings.Value,
                    settings.Type ?? "String",
                    cancellationToken
                )
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine($"[green bold]✓[/] Parameter '[bold]{settings.Name}[/]' saved");
            return 0;
        }
    }

    public class RmCommand : Command<NameSettings>
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
            NameSettings settings,
            CancellationToken cancellationToken
        )
        {
            VerboseConfig.Enabled = settings.Verbose;
            var client = _clientFactory.CreateSsmClient(settings.EndpointUrl);
            SsmOperations
                .DeleteParameterAsync(client, settings.Name, cancellationToken)
                .GetAwaiter()
                .GetResult();
            _console.MarkupLine(
                $"[green bold]✓[/] Parameter '[bold]{settings.Name}[/]' deleted"
            );
            return 0;
        }
    }
}
