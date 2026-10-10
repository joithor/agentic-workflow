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
using Aw.BugFix.Features.Status;
using Aw.Kernel;

namespace Aw.BugFix.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3), used through AwJson.CreateOptions.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(Ticket))]
[JsonSerializable(typeof(BugFixState))]
[JsonSerializable(typeof(RunRecord))]
[JsonSerializable(typeof(ResolutionCheckInput))]
[JsonSerializable(typeof(InitRequest))]
[JsonSerializable(typeof(InitResponse))]
[JsonSerializable(typeof(AdvanceInvestigateRequest))]
[JsonSerializable(typeof(AdvanceInvestigateResponse))]
[JsonSerializable(typeof(AdvanceReproduceRequest))]
[JsonSerializable(typeof(AdvanceReproduceResponse))]
[JsonSerializable(typeof(StartAttemptRequest))]
[JsonSerializable(typeof(StartAttemptResponse))]
[JsonSerializable(typeof(RecordCandidateRequest))]
[JsonSerializable(typeof(RecordCandidateResponse))]
[JsonSerializable(typeof(RecordRunRequest))]
[JsonSerializable(typeof(RecordRunResponse))]
[JsonSerializable(typeof(JudgeCandidateRequest))]
[JsonSerializable(typeof(JudgeCandidateResponse))]
[JsonSerializable(typeof(AdvanceReportRequest))]
[JsonSerializable(typeof(AdvanceReportResponse))]
[JsonSerializable(typeof(RunTestRequest))]
[JsonSerializable(typeof(RunTestResponse))]
[JsonSerializable(typeof(RunUiRequest))]
[JsonSerializable(typeof(RunUiResponse))]
[JsonSerializable(typeof(ResumeRequest))]
[JsonSerializable(typeof(ResumeResponse))]
[JsonSerializable(typeof(StatusRequest))]
internal sealed partial class BugFixJsonContext : JsonSerializerContext;
