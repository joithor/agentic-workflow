using System.Text.Json.Serialization;

namespace Aw.Kernel.AotSmoke;

[JsonSerializable(typeof(Sample))]
[JsonSerializable(typeof(Scalars))]
internal sealed partial class SmokeContext : JsonSerializerContext;
