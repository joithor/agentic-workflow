using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;

namespace Aw.Context.Tests.Eval;

/// <summary>The probe file and report types for tests (in production the Sindri host's JSON context holds them).</summary>
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
internal sealed partial class EvalJsonContext : JsonSerializerContext;
