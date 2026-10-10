using System.Text.Json.Serialization;
using Aw.Kernel.Events;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.Tests;

[JsonSerializable(typeof(NodeJsonFixture))]
[JsonSerializable(typeof(Stamp))]
[JsonSerializable(typeof(Named))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(OutboxCommand))]
[JsonSerializable(typeof(EventEnvelope<Shapes.ItemStarted>))]
[JsonSerializable(typeof(Shapes.Scalars))]
[JsonSerializable(typeof(Shapes.When))]
[JsonSerializable(typeof(Shapes.Wrapped))]
[JsonSerializable(typeof(Shapes.Listed))]
[JsonSerializable(typeof(Shapes.InitOnly))]
[JsonSerializable(typeof(Shapes.RequiredInit))]
[JsonSerializable(typeof(Shapes.DerivedShape))]
[JsonSerializable(typeof(Shapes.OrderedDerived))]
[JsonSerializable(typeof(Dictionary<string, int>))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
