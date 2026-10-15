namespace Aw.Meta.Core.Gate;

/// <summary>Classifies a red gate test as a flake or a real failure.</summary>
public static class FlakeRule
{
    /// <summary>Classifies the test.</summary>
    /// <param name="alone">The test rerun alone on the gate tree.</param>
    /// <param name="onMain">The test run on a plain main tree.</param>
    /// <param name="gateFailure">The failure text from the gate run.</param>
    /// <returns>Flake when it passes alone or fails the same way on main; otherwise real.</returns>
    public static FlakeVerdict Classify(RerunOutcome alone, RerunOutcome onMain, string gateFailure) =>
        throw new NotSupportedException("FlakeRule: make FlakeRuleTests pass");
}
