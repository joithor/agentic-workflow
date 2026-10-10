using System.Text.Json.Serialization;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Tests;

[JsonSerializable(typeof(NodeJsonFixture))]
[JsonSerializable(typeof(Stamp))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(OutboxCommand))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
