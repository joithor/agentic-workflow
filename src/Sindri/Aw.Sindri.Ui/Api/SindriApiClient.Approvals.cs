using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The approve-all routes (<see cref="SindriApiClient"/>, continued): the plan, the app challenge, the approval and the job it runs as. A challenge and the approval are sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ApproveAllPlan>> ApproveAllPlan(Uri api, CancellationToken cancellationToken) => Get<ApproveAllPlan>(api, WorkflowRoutes.ApproveAll, cancellationToken);

    /// <inheritdoc />
    public Task<Result<AppChallengeView>> IssueAppChallenge(Uri api, AppChallengeRequest request, CancellationToken cancellationToken) =>
        Call<AppChallengeView>(api, HttpMethod.Post, WorkflowRoutes.AppChallenge, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<ApproveAllStarted>> ApproveAll(Uri api, ApproveAllRequest request, CancellationToken cancellationToken) =>
        Call<ApproveAllStarted>(api, HttpMethod.Post, WorkflowRoutes.ApproveAll, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<ApproveAllJob>> ApproveAllJob(Uri api, string id, CancellationToken cancellationToken) => Get<ApproveAllJob>(api, WorkflowRoutes.Job(id), cancellationToken);
}
