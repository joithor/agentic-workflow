using System.Text.Json.Serialization;

namespace Aw.Kernel.Tests;

[JsonSerializable(typeof(NodeJsonFixture))]
[JsonSerializable(typeof(Stamp))]
[JsonSerializable(typeof(double[]))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

internal sealed record Stamp(DateTimeOffset At);
