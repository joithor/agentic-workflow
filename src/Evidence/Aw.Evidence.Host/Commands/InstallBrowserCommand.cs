using System.Text.Json;
using Aw.Evidence.Features.InstallBrowser;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host.Commands;

/// <summary>The 'install-browser' command: installs the browser the run and parity commands drive.</summary>
internal static class InstallBrowserCommand
{
    internal const string Name = "install-browser";
    internal const string Shape = "install-browser";

    private static readonly CliSpec _spec = new(0, [], []);

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json, CancellationToken cancellationToken)
    {
        if (CliArguments.Parse(args, _spec) is null)
        {
            return CommandOutput.Usage(Shape);
        }

        var service = services.GetRequiredService<IService<InstallBrowserRequest, InstallBrowserResponse>>();
        var result = await service.Handle(new InstallBrowserRequest(), cancellationToken).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Error(result.Error) : CommandOutput.Json(result.Value, json);
    }
}
