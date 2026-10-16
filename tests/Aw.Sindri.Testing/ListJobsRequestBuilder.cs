using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListJobsRequest with deterministic defaults: no filter.</summary>
public sealed class ListJobsRequestBuilder
{
    private JobState? _state;
    private JobKind? _kind;

    /// <summary>Filters by state.</summary>
    /// <param name="value">The state, or null.</param>
    /// <returns>This builder.</returns>
    public ListJobsRequestBuilder InState(JobState? value)
    {
        _state = value;
        return this;
    }

    /// <summary>Filters by kind.</summary>
    /// <param name="value">The kind, or null.</param>
    /// <returns>This builder.</returns>
    public ListJobsRequestBuilder OfKind(JobKind? value)
    {
        _kind = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListJobsRequest Build() => new(_state, _kind);
}
