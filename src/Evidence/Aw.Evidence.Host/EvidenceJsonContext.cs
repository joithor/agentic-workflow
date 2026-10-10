using System.Text.Json.Serialization;
using Aw.Evidence.Features.RunScript;
using Aw.Kernel;

namespace Aw.Evidence.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(RunScriptRequest))]
[JsonSerializable(typeof(RunScriptResponse))]
internal sealed partial class EvidenceJsonContext : JsonSerializerContext;
