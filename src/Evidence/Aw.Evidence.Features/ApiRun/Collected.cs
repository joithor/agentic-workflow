using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>What the run's steps produced so far: the steps, the failed assertions, how many assertions there were, the first failure's position and an error that ended the run.</summary>
internal sealed class Collected
{
    internal List<ApiStep> Steps { get; } = [];

    internal List<ApiFailure> Failures { get; } = [];

    internal int Assertions { get; set; }

    internal int? FirstFailedAssertion { get; set; }

    internal string? FirstFailureText { get; set; }

    internal CodedMessage? Error { get; set; }
}
