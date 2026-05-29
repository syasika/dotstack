using Spectre.Console.Cli;

namespace DotStack.Cli.Tests;

#pragma warning disable CS8619

/// <summary>
/// Minimal IRemainingArguments stub for test CommandContext construction.
/// </summary>
internal sealed class FakeRemainingArguments : IRemainingArguments
{
    public ILookup<string, string?> Parsed { get; } = Enumerable.Empty<string>().ToLookup(_ => "");
    public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
}

/// <summary>
/// Concrete CommandSettings subclass for commands typed to Command&lt;CommandSettings&gt;.
/// </summary>
public class ConcreteCommandSettings : CommandSettings { }

internal static class TestHelpers
{
    private static readonly FakeRemainingArguments FakeArgs = new();

    public static CommandContext CreateContext(string commandName = "test")
    {
        return new CommandContext([], FakeArgs, commandName, null);
    }
}

public static class CommandExtensions
{
    /// <summary>
    /// Invoke a Spectre.Console.Cli command synchronously in tests.
    /// Calls through ICommand&lt;TSettings&gt;.ExecuteAsync with a default context and cancellation token.
    /// </summary>
    public static int Execute<TSettings>(
        this Command<TSettings> cmd,
        TSettings settings,
        string commandName = "test")
        where TSettings : CommandSettings
    {
        return cmd.Execute(settings, CancellationToken.None, commandName);
    }

    /// <summary>
    /// Invoke a Spectre.Console.Cli command synchronously with a specific CancellationToken.
    /// </summary>
    public static int Execute<TSettings>(
        this Command<TSettings> cmd,
        TSettings settings,
        CancellationToken cancellationToken,
        string commandName = "test")
        where TSettings : CommandSettings
    {
        var ctx = TestHelpers.CreateContext(commandName);
        return ((ICommand<TSettings>)cmd)
            .ExecuteAsync(ctx, settings, cancellationToken)
            .GetAwaiter().GetResult();
    }
}

#pragma warning restore CS8619
