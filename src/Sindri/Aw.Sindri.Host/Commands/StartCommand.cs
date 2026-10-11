using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Bare 'sindri' and 'sindri --headless' (factory addendum §17.29.1): start the control plane if it isn't running
/// (detached, through the launcher), open the app unless headless (<paramref name="openUi"/> is null), then print the
/// banner and the summary. Exit 1 when the profile needs approval, so a script sees the attention; the app never changes
/// the exit code.
/// </summary>
internal sealed class StartCommand(IControlPlaneProbe probe, IControlPlaneLauncher launcher, ControlPlaneClient client, Output output, Func<string>? openUi)
{
    private static readonly TimeSpan _startBudget = TimeSpan.FromSeconds(10);

    internal async Task<int> Run(CancellationToken ct)
    {
        await output.Out.WriteLineAsync(Banner.Text).ConfigureAwait(false);
        var found = await probe.Check(ct).ConfigureAwait(false);
        if (found.IsError)
        {
            found = await Launch(ct).ConfigureAwait(false);
            if (found.IsError)
            {
                return await output.Error(found.Error).ConfigureAwait(false);
            }
        }

        var status = await client.Get<GetStatusResponse>("/sindri/status", ct).ConfigureAwait(false);
        if (status.IsError)
        {
            return await output.Error(status.Error).ConfigureAwait(false);
        }

        var ui = openUi?.Invoke();
        return await output.Text(SummaryText.Render(status.Value, found.Value.Endpoint, ui), SummaryText.NeedsApproval(status.Value) ? 1 : 0)
            .ConfigureAwait(false);
    }

    private async Task<Result<(ControlEndpoint Endpoint, Ping Ping)>> Launch(CancellationToken ct)
    {
        var launched = launcher.Launch();
        if (launched.IsError)
        {
            return launched.Error;
        }

        using var child = launched.Value;
        var deadline = DateTimeOffset.UtcNow + _startBudget;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(200, ct).ConfigureAwait(false);
            var found = await probe.Check(ct).ConfigureAwait(false);
            if (found.IsSuccess)
            {
                return found;
            }

            if (child.HasExited)
            {
                return SindriErrors.StartFailed.With($"'sindri serve' exited: {child.LastError()} (log: {child.LogPath})");
            }
        }

        return SindriErrors.StartFailed.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"'sindri serve' didn't answer within {_startBudget.TotalSeconds:0} s (log: {child.LogPath})"));
    }
}
