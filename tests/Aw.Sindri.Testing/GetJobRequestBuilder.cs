using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetJobRequest with deterministic defaults: A.FirstJobId.</summary>
public sealed class GetJobRequestBuilder
{
    private JobId _job = A.FirstJobId;

    /// <summary>Sets the job.</summary>
    /// <param name="value">The job.</param>
    /// <returns>This builder.</returns>
    public GetJobRequestBuilder ForJob(JobId value)
    {
        _job = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetJobRequest Build() => new(_job);
}
