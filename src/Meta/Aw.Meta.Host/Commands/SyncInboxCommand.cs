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
        if (args.Count != 1
            || !MessageId.TryParse(args[0], CultureInfo.InvariantCulture, out var messageId))
        {
            return CommandOutput.Usage("inbox-sync <messageId>");
        }

        var service = services.GetRequiredService<IService<SyncInboxRequest, SyncInboxResponse>>();
        var result = await service.Handle(new SyncInboxRequest(messageId), CancellationToken.None).ConfigureAwait(false);
        return CommandOutput.Write(result, json);
    }
}
