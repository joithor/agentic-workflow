using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the whole map's SND-CONTEXT-030 refusal says about why there is no map to draw: a build is running, or there is none yet. Both members are optional.</summary>
/// <param name="Building">A running build.</param>
/// <param name="NoMap">The no-map facts.</param>
internal sealed record MapNotReadyView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MapBuildingView? Building,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MapNotBuiltView? NoMap)
{
    /// <summary>The refusal with neither member (an older API, or a lens refusal).</summary>
    internal static MapNotReadyView None { get; } = new(Building: null, NoMap: null);

    /// <summary>Reads the two members off a refusal's extensions; a member that does not read is absent.</summary>
    /// <param name="error">The refusal.</param>
    /// <returns>What it carried.</returns>
    internal static MapNotReadyView From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new MapNotReadyView(Member<MapBuildingView>(error, "building"), Member<MapNotBuiltView>(error, "noMap"));
    }

    private static T? Member<T>(Error error, string name)
        where T : class
    {
        var member = error.Extensions?.FirstOrDefault(e => string.Equals(e.Key, name, StringComparison.Ordinal));
        if (member is not { Value.ValueKind: System.Text.Json.JsonValueKind.Object } found)
        {
            return null;
        }

        var read = AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(found.Value.GetRawText()), SindriApiClient.Json);
        return read.IsSuccess ? read.Value : null;
    }
}
