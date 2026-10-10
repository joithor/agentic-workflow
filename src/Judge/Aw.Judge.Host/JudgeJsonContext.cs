using System.Text.Json.Serialization;
using Aw.Judge.Features.GetBrief;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Features.SaveBrief;
using Aw.Kernel;

namespace Aw.Judge.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(GetHealthRequest))]
[JsonSerializable(typeof(GetHealthResponse))]
[JsonSerializable(typeof(SaveBriefRequest))]
[JsonSerializable(typeof(SaveBriefResponse))]
[JsonSerializable(typeof(GetBriefRequest))]
[JsonSerializable(typeof(GetBriefResponse))]
internal sealed partial class JudgeJsonContext : JsonSerializerContext;
