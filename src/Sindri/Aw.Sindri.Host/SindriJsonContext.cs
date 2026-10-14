using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.RebuildViews;
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
[JsonSerializable(typeof(ListOutboxResponse))]   // engine P6 unit (c): outbox list
[JsonSerializable(typeof(EffectRow))]            // engine P6: an effect's ledger row (§5.7)
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
internal sealed partial class SindriJsonContext : JsonSerializerContext;
