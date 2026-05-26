using DotStack.Tui;
using Spectre.Console.Cli;

namespace DotStack.Cli.Commands;

public class BrowseCommand : Command<EndpointSettings>
{
    protected override int Execute(
        CommandContext context,
        EndpointSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        using var dashboard = new BrowseDashboard(settings.EndpointUrl);
        dashboard.Run();
        return 0;
    }
}
