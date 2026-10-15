using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The follower's re-read of the status and the services view (<see cref="ControlPlaneFollower"/>, continued).</summary>
internal sealed partial class ControlPlaneFollower
{
    // Re-reads the status and the services view. "Views behind" (SND-API-011, the server's 2 s wait after an
    // X-Sindri-After) is not trouble: the last data stays and the next beat reads again. A projection that is still
    // catching up (cursor behind the ledger head) or rebuilding is read again too, since no further event need come; a
    // stalled one won't move by itself, so it waits for an event. A failed status ends the follow.
    private async Task<bool> Refresh(Uri url, CancellationToken cancellationToken)
    {
        _again = false;
        var status = await api.Status(url, cancellationToken).ConfigureAwait(true);
        if (status.IsError)
        {
            if (IsBehind(status.Error))
            {
                _again = true;
                return true;
            }

            _ = health.Failed(status.Error, url);
            return false;
        }

        health.Show(url, status.Value);
        _again = status.Value.Views is { Stalled: false } views && (views.Rebuilding || views.Cursor < views.LedgerHead);
        var services = await api.Services(url, cancellationToken).ConfigureAwait(true);
        if (services.IsSuccess)
        {
            health.ShowViews(services.Value);
        }
        else if (IsBehind(services.Error))
        {
            _again = true;
        }
        else if (string.Equals(services.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            health.ShowViews(view: null);   // before P2 there's no such route: no banner
        }

        return true;
    }

    private static bool IsBehind(Error error) => string.Equals(error.Code, SindriApiClient.ViewsBehind, StringComparison.Ordinal);
}
