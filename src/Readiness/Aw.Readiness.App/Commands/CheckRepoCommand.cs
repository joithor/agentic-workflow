using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Readiness.App.Features.CheckRepo;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.App.Commands;

/// <summary>The 'check' command: binds its arguments, calls CheckRepo and maps the result to an exit code (spec §8):
/// 0 when the target level holds, 1 when it doesn't, 2 on a usage or tool error.</summary>
internal static class CheckRepoCommand
{
    internal const string Name = "check";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        var parsed = CheckArgs.Parse(args);
        if (parsed.IsError)
        {
            return ToolError(parsed.Error, json);
        }

        var service = services.GetRequiredService<IService<CheckRepoRequest, CheckRepoResponse>>();
        var request = new CheckRepoRequest(parsed.Value.Root, parsed.Value.UseHost);
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return ToolError(result.Error, json);
        }

        Console.Out.Write(parsed.Value.Json
            ? Encoding.UTF8.GetString(AwJson.Serialize(result.Value, json)) + "\n"
            : ReportText.Render(result.Value));
        return ExitCode(result.Value);
    }

    internal static int ExitCode(CheckRepoResponse report) => report.Holds ? 0 : 1;

    private static int ToolError(Error error, JsonSerializerOptions json)
    {
        Console.Error.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(error, json)));
        return 2;
    }
}
