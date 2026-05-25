namespace DotStack.Tui;

public interface IServicePanel : IDisposable
{
    string HelpText { get; }
    bool IsAtRootLevel { get; }
    bool HandleKey(ConsoleKeyInfo key, CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
    void Render(System.Text.StringBuilder content);
}
