using System.Text.Json.Serialization;
using Aw.Judge.Features.ExplainDecision;
using Aw.Judge.Features.GetBrief;
using Aw.Judge.Features.GetHealth;
using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Judge.Features.MapBriefByName;
using Aw.Judge.Features.SaveBrief;
using Aw.Judge.Features.SetBriefAgentId;
using Aw.Judge.Features.UndoDecision;
using Aw.Judge.Host.Commands;
using Aw.Kernel;

namespace Aw.Judge.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(HealthOutput))]
[JsonSerializable(typeof(BriefOutput))]
[JsonSerializable(typeof(DecisionOutput))]
[JsonSerializable(typeof(GetHealthRequest))]
[JsonSerializable(typeof(GetHealthResponse))]
[JsonSerializable(typeof(SaveBriefRequest))]
[JsonSerializable(typeof(SaveBriefResponse))]
[JsonSerializable(typeof(GetBriefRequest))]
[JsonSerializable(typeof(GetBriefResponse))]
[JsonSerializable(typeof(MapBriefByNameRequest))]
[JsonSerializable(typeof(MapBriefByNameResponse))]
[JsonSerializable(typeof(MapBriefByDispatchRequest))]
[JsonSerializable(typeof(MapBriefByDispatchResponse))]
[JsonSerializable(typeof(SetBriefAgentIdRequest))]
[JsonSerializable(typeof(SetBriefAgentIdResponse))]
[JsonSerializable(typeof(ExplainDecisionRequest))]
[JsonSerializable(typeof(ExplainDecisionResponse))]
[JsonSerializable(typeof(UndoDecisionRequest))]
[JsonSerializable(typeof(UndoDecisionResponse))]
internal sealed partial class JudgeJsonContext : JsonSerializerContext;
