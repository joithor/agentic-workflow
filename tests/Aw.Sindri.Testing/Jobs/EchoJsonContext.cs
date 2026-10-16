using System.Text.Json.Serialization;

namespace Aw.Sindri.Testing.Jobs;

/// <summary>The echo job's source-generated JSON types (API P6): the test host adds this context's resolver to the package's options, because the echo job is not part of the product's context.</summary>
[JsonSerializable(typeof(EchoJobRequest))]
[JsonSerializable(typeof(EchoReport))]
public sealed partial class EchoJsonContext : JsonSerializerContext;
