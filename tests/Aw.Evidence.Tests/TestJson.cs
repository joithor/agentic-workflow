using System.Text;
using System.Text.Json;
using Aw.Evidence.Host;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests;

/// <summary>The package's real JSON options (AwJson over the Host's context), as Composition builds them.</summary>
internal static class TestJson
{
    internal static JsonSerializerOptions Options { get; } = AwJson.CreateOptions(EvidenceJsonContext.Default);

    internal static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    internal static string Write<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, Options));
}
