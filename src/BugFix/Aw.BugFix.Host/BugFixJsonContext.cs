using System.Text.Json.Serialization;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceInvestigate;
using Aw.BugFix.Features.AdvanceReport;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.Init;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.RecordCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.Resume;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.RunUi;
using Aw.BugFix.Features.StartAttempt;

namespace Aw.BugFix.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3), used through AwJson.CreateOptions.</summary>
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(BugFixState))]
[JsonSerializable(typeof(RunRecord))]
[JsonSerializable(typeof(ResolutionCheckInput))]
[JsonSerializable(typeof(InitResponse))]
[JsonSerializable(typeof(AdvanceInvestigateResponse))]
[JsonSerializable(typeof(AdvanceReproduceResponse))]
[JsonSerializable(typeof(StartAttemptResponse))]
[JsonSerializable(typeof(RecordCandidateResponse))]
[JsonSerializable(typeof(RecordRunResponse))]
[JsonSerializable(typeof(JudgeCandidateResponse))]
[JsonSerializable(typeof(AdvanceReportResponse))]
[JsonSerializable(typeof(RunTestResponse))]
[JsonSerializable(typeof(RunUiResponse))]
[JsonSerializable(typeof(ResumeResponse))]
internal sealed partial class BugFixJsonContext : JsonSerializerContext;
