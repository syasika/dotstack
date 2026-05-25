using Spectre.Console.Cli;
using DotStack.Tui;

namespace DotStack.Cli.Commands;

public class BrowseCommand : Command<EndpointSettings>
{
    protected override int Execute(CommandContext context, EndpointSettings settings, CancellationToken cancellationToken = default)
    {
        using var dashboard = new BrowseDashboard(settings.EndpointUrl);
        dashboard.Run();
        return 0;
    }
}
