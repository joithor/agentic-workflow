using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// The desk's notices (engine spec §6.3; README X5, X50; engine P6 unit (c)): each is one JSON line of
/// '$AW_SINDRI_HOME/notifications.jsonl', at most once per effect identity, so an activity retry or a re-emission writes
/// nothing twice. An interrupting notice is then shown through <see cref="IOsNotifier"/>; an info one isn't, and is
/// collected into the day's digest ('digest/&lt;yyyy-MM-dd&gt;.jsonl', UTC). The line is written before the banner, so a
/// crash between them loses the banner, never the record. Sends are serialized within the process; only serve's worker
/// sends.
/// </summary>
/// <param name="settings">The home.</param>
/// <param name="os">The OS notification centre.</param>
/// <param name="json">The package's AwJson options.</param>
internal sealed class FileDeskNotices(SindriSettings settings, IOsNotifier os, JsonSerializerOptions json) : IDeskNotices, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Dispose() => _gate.Dispose();

    public async ValueTask<Result<bool>> Send(DeskNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await Sent(notice.Identity, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            OwnerOnly.EnsureDirectory(settings.Home);
            var line = Line(notice);
            await OwnerOnly.Append(settings.NotificationsPath, line, cancellationToken).ConfigureAwait(false);
            if (notice.Class == NoticeClass.Interrupt)
            {
                await os.Show("Sindri: " + notice.Instance.Value, notice.Text, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                OwnerOnly.EnsureDirectory(settings.DigestDirectory);
                var day = notice.At.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                await OwnerOnly.Append(Path.Combine(settings.DigestDirectory, day + ".jsonl"), line, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Whether a line of the file already names <paramref name="identity"/>; a line that isn't a notice is skipped.</summary>
    private async Task<bool> Sent(string identity, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.NotificationsPath))
        {
            return false;
        }

        foreach (var line in await File.ReadAllLinesAsync(settings.NotificationsPath, cancellationToken).ConfigureAwait(false))
        {
            if (line.Length > 0 && AwJson.Deserialize<DeskNotice>(Encoding.UTF8.GetBytes(line), json) is { IsError: false } read
                && string.Equals(read.Value.Identity, identity, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private byte[] Line(DeskNotice notice) => [.. AwJson.Serialize(notice, json), (byte)'\n'];
}
