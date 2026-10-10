using System.Text.Json.Serialization;
using Aw.Kernel.Events;
using Aw.Kernel.Outbox;

namespace Aw.Kernel.AotSmoke;

[JsonSerializable(typeof(Sample))]
[JsonSerializable(typeof(OutboxCommand))]
[JsonSerializable(typeof(EventEnvelope<Sample>))]
internal sealed partial class SmokeContext : JsonSerializerContext;
