using System.Text.Json.Serialization;
using Aw.Kernel.Events;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Tests;

[JsonSerializable(typeof(Stamp))]
[JsonSerializable(typeof(Named))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(OutboxCommand))]
[JsonSerializable(typeof(EventEnvelope<Shapes.ItemStarted>))]
[JsonSerializable(typeof(Shapes.Profile))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(ErrorExtensionsTests.Anchor[]))]
[JsonSerializable(typeof(Shapes.When))]
[JsonSerializable(typeof(Shapes.Listed))]
[JsonSerializable(typeof(Shapes.InitOnly))]
[JsonSerializable(typeof(Shapes.RequiredInit))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
