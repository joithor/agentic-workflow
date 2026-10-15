using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowRecipe;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Probe;
using Aw.Sindri.Host.Commands.Recipe;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(SindriProfile))]
[JsonSerializable(typeof(ControlEndpoint))]
[JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(ProcessStarted))]
[JsonSerializable(typeof(ProcessStopped))]
[JsonSerializable(typeof(WorkflowEventPayload))]
[JsonSerializable(typeof(ProfileApproved))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(CommandHandled))]
[JsonSerializable(typeof(ServiceRan))]
[JsonSerializable(typeof(ProfileChecked))]
[JsonSerializable(typeof(ViewsRebuilt))]
[JsonSerializable(typeof(AsOf))]
[JsonSerializable(typeof(ViewsStatus))]
[JsonSerializable(typeof(ServiceRow))]
[JsonSerializable(typeof(ProfileRow))]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(CommandAccepted))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(StopProcessResponse))]
[JsonSerializable(typeof(ApproveProfileRequest))]
[JsonSerializable(typeof(ApproveProfileResponse))]
[JsonSerializable(typeof(ValidateProfileResponse))]
[JsonSerializable(typeof(RunDoctorResponse))]
[JsonSerializable(typeof(EventView))]
[JsonSerializable(typeof(ListCommandsRequest))]
[JsonSerializable(typeof(ListCommandsResponse))]
[JsonSerializable(typeof(CommandListRow))]
[JsonSerializable(typeof(ListServicesRequest))]
[JsonSerializable(typeof(ListServicesResponse))]
[JsonSerializable(typeof(ServiceView))]
[JsonSerializable(typeof(GetProfileRequest))]
[JsonSerializable(typeof(GetProfileResponse))]
[JsonSerializable(typeof(RebuildViewsRequest))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(BlockDescriptor))]
[JsonSerializable(typeof(ValidateRecipeResponse))]
[JsonSerializable(typeof(EvalRecipeResponse))]
[JsonSerializable(typeof(WorkflowEvent))]
[JsonSerializable(typeof(EvalStartLine))]
[JsonSerializable(typeof(EvalStepLine))]
[JsonSerializable(typeof(RecipeValidationView))]
[JsonSerializable(typeof(CompleteStepResponse))]
[JsonSerializable(typeof(InstanceView))]
[JsonSerializable(typeof(GetInstanceResponse))]
[JsonSerializable(typeof(ListInstancesResponse))]
[JsonSerializable(typeof(GetInstanceEventsResponse))]
[JsonSerializable(typeof(InstanceEventView))]
[JsonSerializable(typeof(InstanceEventFields))]
[JsonSerializable(typeof(StartInstanceResponse))]
[JsonSerializable(typeof(StartInstanceRequest))]
[JsonSerializable(typeof(ApplyCommandRequest))]
[JsonSerializable(typeof(CompleteStepRequest))]
[JsonSerializable(typeof(ApplyCommandResponse))]
[JsonSerializable(typeof(IssueNonceResponse))]
[JsonSerializable(typeof(RecipeRegistered))]
[JsonSerializable(typeof(RecipeApproved))]
[JsonSerializable(typeof(RecipeRevoked))]
[JsonSerializable(typeof(RegisterRecipeRequest))]
[JsonSerializable(typeof(RegisterRecipeResponse))]
[JsonSerializable(typeof(ListRecipesResponse))]
[JsonSerializable(typeof(ShowRecipeResponse))]
[JsonSerializable(typeof(ApproveRecipeRequest))]
[JsonSerializable(typeof(ApproveRecipeResponse))]
[JsonSerializable(typeof(RevokeRecipeRequest))]
[JsonSerializable(typeof(RevokeRecipeResponse))]
[JsonSerializable(typeof(IssueChallengeRequest))]
[JsonSerializable(typeof(IssueChallengeResponse))]
[JsonSerializable(typeof(HarnessProbeReport))]
[JsonSerializable(typeof(ChallengeAnswer))]
[JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(NodeView))]
[JsonSerializable(typeof(NeighborsView))]
[JsonSerializable(typeof(PathView))]
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
[JsonSerializable(typeof(ProbeRun))]
[JsonSerializable(typeof(SourcesView))]
[JsonSerializable(typeof(McpToolError))]
[JsonSerializable(typeof(McpSourcesAnswer))]
[JsonSerializable(typeof(RegisterMcpResponse))]
[JsonSerializable(typeof(ContextSourcesSynced))]
[JsonSerializable(typeof(ContextMapImported))]
[JsonSerializable(typeof(MapDocument))]
[JsonSerializable(typeof(MapStatusView))]
[JsonSerializable(typeof(MapExported))]
[JsonSerializable(typeof(MapHealth))]
[JsonSerializable(typeof(PurgeReport))]
[JsonSerializable(typeof(ImportContextMapRequest))]
[JsonSerializable(typeof(ImportContextMapResponse))]
[JsonSerializable(typeof(BudgetFigures))]
[JsonSerializable(typeof(ChildGroup))]
[JsonSerializable(typeof(ChildRow))]
[JsonSerializable(typeof(CurrentState))]
[JsonSerializable(typeof(DeskForm))]
[JsonSerializable(typeof(DeskOutput))]
[JsonSerializable(typeof(EngineOwnedBadge))]
[JsonSerializable(typeof(FactPending))]
[JsonSerializable(typeof(FactRef))]
[JsonSerializable(typeof(FactRow))]
[JsonSerializable(typeof(GateLine))]
[JsonSerializable(typeof(InputPresence))]
[JsonSerializable(typeof(InstanceDetail))]
[JsonSerializable(typeof(InstanceList))]
[JsonSerializable(typeof(InstanceRecipe))]
[JsonSerializable(typeof(InstanceSummary))]
[JsonSerializable(typeof(LoopPair))]
[JsonSerializable(typeof(Progress))]
[JsonSerializable(typeof(RecipeInputView))]
[JsonSerializable(typeof(RecipeLoopView))]
[JsonSerializable(typeof(RecipeOutputView))]
[JsonSerializable(typeof(RecipeStateView))]
[JsonSerializable(typeof(RecipeView))]
[JsonSerializable(typeof(Round))]
[JsonSerializable(typeof(StateCell))]
[JsonSerializable(typeof(StatusAnswer))]
[JsonSerializable(typeof(StatusBudget))]
[JsonSerializable(typeof(StatusChild))]
[JsonSerializable(typeof(StatusCommand))]
[JsonSerializable(typeof(StatusDrift))]
[JsonSerializable(typeof(StatusFields))]
[JsonSerializable(typeof(StatusHeader))]
[JsonSerializable(typeof(StatusInput))]
[JsonSerializable(typeof(StatusPaused))]
[JsonSerializable(typeof(StatusTimeout))]
[JsonSerializable(typeof(StepDeclaration))]
[JsonSerializable(typeof(SubjectRef))]
[JsonSerializable(typeof(ViewCounts))]
[JsonSerializable(typeof(Workstream))]
[JsonSerializable(typeof(StateChip))]
[JsonSerializable(typeof(ListCounts))]
[JsonSerializable(typeof(ParentRef))]
[JsonSerializable(typeof(FactView))]
[JsonSerializable(typeof(MapImportChallenge))]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.ActionClass), TypeInfoPropertyName = "LayoutActionClass")]   // why: the command catalog has an ActionClass too
// why: the engine layout records (Engine.Runtime.Layout) share simple names with the API view records (Workflow.Views)
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.AnswerKind), TypeInfoPropertyName = "LayoutAnswerKind")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.ChildGroup), TypeInfoPropertyName = "LayoutChildGroup")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.ChildRow), TypeInfoPropertyName = "LayoutChildRow")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.FactDirection), TypeInfoPropertyName = "LayoutFactDirection")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.FactRef), TypeInfoPropertyName = "LayoutFactRef")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.FactRow), TypeInfoPropertyName = "LayoutFactRow")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.FactStatus), TypeInfoPropertyName = "LayoutFactStatus")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.LoopPair), TypeInfoPropertyName = "LayoutLoopPair")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.StateCell), TypeInfoPropertyName = "LayoutStateCell")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.StateGlyph), TypeInfoPropertyName = "LayoutStateGlyph")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.StatusFields), TypeInfoPropertyName = "LayoutStatusFields")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.SubjectRef), TypeInfoPropertyName = "LayoutSubjectRef")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.TimeoutKind), TypeInfoPropertyName = "LayoutTimeoutKind")]
[JsonSerializable(typeof(Core.Workflow.Engine.Runtime.Layout.Workstream), TypeInfoPropertyName = "LayoutWorkstream")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.ChildGroup>), TypeInfoPropertyName = "LayoutListChildGroup")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.ChildRow>), TypeInfoPropertyName = "LayoutListChildRow")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.FactRow>), TypeInfoPropertyName = "LayoutListFactRow")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.LoopPair>), TypeInfoPropertyName = "LayoutListLoopPair")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.StateCell>), TypeInfoPropertyName = "LayoutListStateCell")]
[JsonSerializable(typeof(IReadOnlyList<Core.Workflow.Engine.Runtime.Layout.Workstream>), TypeInfoPropertyName = "LayoutListWorkstream")]
internal sealed partial class SindriJsonContext : JsonSerializerContext;
