using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Eval;

/// <summary>The probe file and report types for tests (in production the Sindri host's JSON context holds them).</summary>
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
internal sealed partial class EvalJsonContext : JsonSerializerContext
{
    public static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(Default!);
}
