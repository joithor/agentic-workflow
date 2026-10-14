using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>The desk's notices as '$AW_SINDRI_HOME/notifications.jsonl' and OS notifications (engine spec §6.3). Engine P6 unit (c).</summary>
/// <param name="settings">The home.</param>
/// <param name="os">The OS notification centre.</param>
/// <param name="json">The package's AwJson options.</param>
internal sealed class FileDeskNotices(SindriSettings settings, IOsNotifier os, JsonSerializerOptions json) : IDeskNotices
{
    public ValueTask<Result<bool>> Send(DeskNotice notice, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P6 unit (c): notifications.jsonl under {settings.Home} ({os}, {json.WriteIndented})");
}
