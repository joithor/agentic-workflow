using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The first-run offer of bare `sindri` (spec O6): on a box where Onyx isn't set up and the approved profile names no Onyx of its own, a human at a terminal is asked "Set up Onyx now? [Y/n]" before the control plane starts.
/// Yes runs `sindri onyx up` in that terminal; no is remembered (the `offer-declined` marker), so the offer isn't repeated until the human runs `sindri onyx up`, and the needs-you list carries the row. Not at a terminal, or in an agent
/// session: no offer, only the row. Whatever happens, Sindri starts afterwards.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="session">Who runs the CLI.</param>
/// <param name="prompt">Asks the question and returns the answer line (null at end of input).</param>
/// <param name="up">Runs `sindri onyx up` and returns its exit code.</param>
/// <param name="error">Where the offer's lines go.</param>
internal sealed class OnyxOffer(IServiceProvider services, AgentSession session, Func<string, Task<string?>> prompt, Func<CancellationToken, Task<int>> up, TextWriter error)
{
    internal async Task Run(CancellationToken ct)
    {
        if (!await Wanted(ct).ConfigureAwait(false))
        {
            return;
        }

        var line = await prompt("Set up Onyx (Sindri's business-context search) now? [Y/n] ").ConfigureAwait(false);
        if (line is null)
        {
            return;   // end of input is no answer: neither a start nor a remembered no
        }

        var answer = line.Trim();
        if (answer is { Length: > 0 } && !answer.StartsWith('y') && !answer.StartsWith('Y'))
        {
            var workspace = services.GetRequiredService<IOnyxWorkspace>();
            try
            {
                _ = workspace.WriteIfChanged(services.GetRequiredService<OnyxFacts>().Paths.Declined, string.Empty, secret: false);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the marker couldn't be written: the offer comes again next time
            }

            await error.WriteLineAsync("Skipped. Set Onyx up any time with `sindri onyx up` (the needs-you list reminds you).").ConfigureAwait(false);
        }
        else if (!await Up(ct).ConfigureAwait(false))
        {
            await error.WriteLineAsync("Onyx setup didn't finish; starting Sindri anyway. Run `sindri onyx up` to continue.").ConfigureAwait(false);
        }
    }

    private async Task<bool> Up(CancellationToken ct)
    {
        try
        {
            return await up(ct).ConfigureAwait(false) == 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;   // whatever happens, Sindri starts afterwards
        }
    }

    // A human, Onyx not set up, nobody said no before, and the approved profile names no Onyx of any kind.
    private async Task<bool> Wanted(CancellationToken ct)
    {
        if (!session.Human)
        {
            return false;
        }

        var workspace = services.GetRequiredService<IOnyxWorkspace>();
        var paths = services.GetRequiredService<OnyxFacts>().Paths;
        var setUp = workspace.ReadState()?.Steps.ContainsKey(OnyxSteps.Start) == true;
        if (setUp || workspace.Read(paths.Declined) is not { IsSuccess: true, Value: null })
        {
            return false;   // set up already, or the human said no before (or the marker can't be read: don't ask over it)
        }

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<IManagedOnyx>().IsConfigured(ct).ConfigureAwait(false) is { IsSuccess: true, Value: false };
        }
    }
}
