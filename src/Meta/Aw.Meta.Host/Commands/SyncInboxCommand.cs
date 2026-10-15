using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.SyncInbox;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'inbox-sync' command: binds its arguments, calls SyncInbox and maps the result to an exit code.</summary>
internal static class SyncInboxCommand
{
    internal const string Name = "inbox-sync";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 4
            || Option(args, "--session") is not { } session
            || Option(args, "--transcript") is not { } transcript
            || !SessionId.TryParse(session, CultureInfo.InvariantCulture, out var sessionId))
        {
            return CommandOutput.Usage("inbox-sync --session <uuid> --transcript <path>");
        }

        var service = services.GetRequiredService<IService<SyncInboxRequest, SyncInboxResponse>>();
        var result = await service.Handle(new SyncInboxRequest(sessionId, transcript), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }

    private static string? Option(IReadOnlyList<string> args, string name)
    {
        var at = args.ToList().IndexOf(name);
        return at >= 0 && at + 1 < args.Count && !args[at + 1].StartsWith("--", StringComparison.Ordinal) ? args[at + 1] : null;
    }
}
