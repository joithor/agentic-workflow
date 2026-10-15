using System.Text;
using System.Text.Json;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>A proposal's payload as the JSON text map_override.payload holds.</summary>
internal static class GraphPayload
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(GraphJsonContext.Default);

    public static string? Write(ProposalPayload? payload) =>
        payload is null ? null : Encoding.UTF8.GetString(AwJson.Serialize(payload, _options));

    public static string WriteSurfaces(IReadOnlyList<ProposedSurface> surfaces) => Encoding.UTF8.GetString(AwJson.Serialize(surfaces.ToArray(), _options));

    public static IReadOnlyList<ProposedSurface> ReadSurfaces(string json)
    {
        var read = AwJson.Deserialize<ProposedSurface[]>(Encoding.UTF8.GetBytes(json), _options);
        return read.IsError ? throw new InvalidOperationException($"proposal_run.surfaces isn't a surface list: {read.Error.Message}") : read.Value;
    }

    public static ProposalPayload? Read(string? json)
    {
        if (json is null)
        {
            return null;
        }

        var read = AwJson.Deserialize<ProposalPayload>(Encoding.UTF8.GetBytes(json), _options);
        return read.IsError ? throw new InvalidOperationException($"map_override.payload isn't a proposal: {read.Error.Message}") : read.Value;
    }
}
