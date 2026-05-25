using Spectre.Console;
using Spectre.Console.Cli;
using DotStack.Core.Aws;
using DotStack.Core.Ssm;

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
        protected override int Execute(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSsmClient(settings.EndpointUrl);
            var parameters = SsmOperations.ListAllParametersAsync(client, cancellationToken).GetAwaiter().GetResult();
            if (parameters.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No parameters.[/]"); return 0; }
            AnsiConsole.MarkupLine($"[bold white on #0066CC] Parameters ({parameters.Count}) [/]");
            foreach (var p in parameters)
                AnsiConsole.MarkupLine($"  [bold #0044CC]{p.Name}[/]  [grey]({p.Type}, v{p.Version})[/]");
            return 0;
        }
    }

    public class GetCommand : Command<NameSettings>
    {
        protected override int Execute(CommandContext context, NameSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSsmClient(settings.EndpointUrl);
            var p = SsmOperations.GetParameterAsync(client, settings.Name, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[bold]Name:[/]  {p.Name}");
            AnsiConsole.MarkupLine($"[bold]Type:[/]  {p.Type}");
            AnsiConsole.MarkupLine($"[bold]Value:[/] {p.Value}");
            AnsiConsole.MarkupLine($"[bold]Version:[/]  v{p.Version}");
            return 0;
        }
    }

    public class PutCommand : Command<PutSettings>
    {
        protected override int Execute(CommandContext context, PutSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSsmClient(settings.EndpointUrl);
            SsmOperations.PutParameterAsync(client, settings.Name, settings.Value, settings.Type ?? "String", cancellationToken)
                .GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Parameter '[bold]{settings.Name}[/]' saved");
            return 0;
        }
    }

    public class RmCommand : Command<NameSettings>
    {
        protected override int Execute(CommandContext context, NameSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateSsmClient(settings.EndpointUrl);
            SsmOperations.DeleteParameterAsync(client, settings.Name, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Parameter '[bold]{settings.Name}[/]' deleted");
            return 0;
        }
    }
}
