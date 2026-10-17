using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CancelJob;

namespace Aw.Sindri.Testing;

/// <summary>Builds a CancelJobRequest with deterministic defaults: A.FirstJobId.</summary>
public sealed class CancelJobRequestBuilder
{
    private JobId _job = A.FirstJobId;

    /// <summary>Sets the job.</summary>
    /// <param name="value">The job.</param>
    /// <returns>This builder.</returns>
    public CancelJobRequestBuilder ForJob(JobId value)
    {
        _job = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CancelJobRequest Build() => new(_job);
}
