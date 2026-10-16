using Aw.Evidence.Core.Data;
using Aw.Kernel;

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

    /// <summary>Ends the run with an error (a request that failed, a program that would not start): the message is scrubbed.</summary>
    internal void Fail(Error error) => Error = new CodedMessage(error.Code, scrubber.Scrub(error.Message));

    /// <summary>Checks a finished step's result against its expectation and records the step, its output and each failed assertion (scrubbed, then capped).</summary>
    internal void Record(StepInfo info, Expectation expect, int code, string output, string? goldenText)
    {
        var checkedExpectation = ExpectationCheck.Evaluate(info.Index, expect, code, output, goldenText);
        var failures = checkedExpectation.Failures.Select(failure => RecordedText.Of(scrubber, failure)).ToList();
        if (checkedExpectation.FirstFailedOrdinal is { } ordinal && FirstFailedAssertion is null)
        {
            FirstFailedAssertion = Assertions + ordinal;
            FirstFailureText = $"{info.Target}: {failures[0].Describe()}";
        }

        Assertions += checkedExpectation.Count;
        Failures.AddRange(failures);

        var status = failures.Count == 0 ? ApiStepStatus.Passed : ApiStepStatus.Failed;
        Steps.Add(new ApiStep(info.Index, info.Action, info.Target, status, code, RecordedText.Of(scrubber, output, RecordedText.OutputLimit), info.HeaderNames));
    }
}
