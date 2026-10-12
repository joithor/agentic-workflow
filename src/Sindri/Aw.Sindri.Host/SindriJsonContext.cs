using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowRecipe;
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
[JsonSerializable(typeof(ProfileApproved))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(CommandHandled))]
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
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(BlockDescriptor))]
[JsonSerializable(typeof(ValidateRecipeResponse))]
[JsonSerializable(typeof(EvalRecipeResponse))]
[JsonSerializable(typeof(WorkflowEvent))]
[JsonSerializable(typeof(EvalStartLine))]
[JsonSerializable(typeof(EvalStepLine))]
[JsonSerializable(typeof(RecipeValidationView))]
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
internal sealed partial class SindriJsonContext : JsonSerializerContext;
