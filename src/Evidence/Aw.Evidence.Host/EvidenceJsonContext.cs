using System.Text.Json.Serialization;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Visual;
using Aw.Evidence.Features.RunScript;
using Aw.Kernel;

namespace Aw.Evidence.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(UiScript))]
[JsonSerializable(typeof(DesignManifest))]
[JsonSerializable(typeof(RunSummary))]
[JsonSerializable(typeof(ParitySummary))]
[JsonSerializable(typeof(VerdictCache))]
[JsonSerializable(typeof(VerdictCacheKey))]
[JsonSerializable(typeof(RunScriptRequest))]
[JsonSerializable(typeof(RunScriptResponse))]
internal sealed partial class EvidenceJsonContext : JsonSerializerContext;
