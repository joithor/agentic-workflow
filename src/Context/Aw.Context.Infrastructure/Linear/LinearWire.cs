using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The AwJson options over <see cref="LinearWireJsonContext"/>.</summary>
internal static class LinearWire
{
    public static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(LinearWireJsonContext.Default);
}
