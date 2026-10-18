using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IOnyxPrompts over the console: questions go to standard error (standard output carries only the summary), a secret is read key by key with echo off and never written anywhere.</summary>
internal sealed class TerminalPrompts : IOnyxPrompts
{
    /// <inheritdoc />
    public bool Interactive => !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;

    /// <inheritdoc />
    public async ValueTask Pause(string message, CancellationToken cancellationToken)
    {
        await Console.Error.WriteAsync($"{message}: ").ConfigureAwait(false);
        _ = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<string?> Ask(string question, CancellationToken cancellationToken)
    {
        await Console.Error.WriteAsync(question).ConfigureAwait(false);
        return (await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false))?.Trim();
    }

    /// <inheritdoc />
    public async ValueTask<string?> AskSecret(string question, CancellationToken cancellationToken)
    {
        await Console.Error.WriteAsync(question).ConfigureAwait(false);
        var typed = new System.Text.StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                await Console.Error.WriteLineAsync().ConfigureAwait(false);
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                _ = typed.Append(key.KeyChar);
            }
        }
    }
}
