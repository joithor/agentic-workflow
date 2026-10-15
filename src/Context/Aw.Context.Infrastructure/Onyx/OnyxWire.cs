using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>The AwJson options over <see cref="OnyxWireJsonContext"/>.</summary>
internal static class OnyxWire
{
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(OnyxWireJsonContext.Default);
}
