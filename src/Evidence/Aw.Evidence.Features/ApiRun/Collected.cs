using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>What the run's steps produced so far: the steps, the failed assertions, how many assertions there were, the first failure and an error that ended the run.</summary>
internal sealed class Collected(ITextScrubber scrubber)
{
    internal List<ApiStep> Steps { get; } = [];

    internal List<ApiFailure> Failures { get; } = [];

    internal int Assertions { get; private set; }

    internal int? FirstFailedAssertion { get; private set; }

    internal string? FirstFailureText { get; private set; }

    internal CodedMessage? Error { get; set; }

    /// <summary>Checks a finished step's result against its expectation and records the step, its output and each failed assertion (scrubbed, then capped).</summary>
    internal void Record(StepInfo info, Expectation expect, int code, string output, string? goldenText)
    {
        var checkedExpectation = ExpectationCheck.Evaluate(info.Index, expect, code, output, goldenText);
        if (checkedExpectation.FirstFailedOrdinal is { } ordinal && FirstFailedAssertion is null)
        {
            FirstFailedAssertion = Assertions + ordinal;
            FirstFailureText = $"{info.Target}: {checkedExpectation.Failures[0].Describe()}";
        }

        Assertions += checkedExpectation.Count;
        foreach (var failure in checkedExpectation.Failures)
        {
            Failures.Add(failure with
            {
                Location = failure.Location is null ? null : RecordedText.Of(scrubber, failure.Location, RecordedText.ValueLimit),
                Expected = RecordedText.Of(scrubber, failure.Expected, RecordedText.ValueLimit),
                Actual = RecordedText.Of(scrubber, failure.Actual, RecordedText.ValueLimit),
            });
        }

        var status = checkedExpectation.Failures.Count == 0 ? ApiStepStatus.Passed : ApiStepStatus.Failed;
        Steps.Add(new ApiStep(info.Index, info.Action, info.Target, status, code, RecordedText.Of(scrubber, output, RecordedText.OutputLimit), info.HeaderNames));
    }
}
