namespace Aw.Evidence.Core.Data;

/// <summary>Checks an expectation against what a request or a CLI call produced.</summary>
public static class ExpectationCheck
{
    /// <summary>Runs every assertion; none stops the others.</summary>
    /// <param name="step">The step's 1-based index.</param>
    /// <param name="expect">The expectation.</param>
    /// <param name="actualCode">The HTTP status or exit code.</param>
    /// <param name="output">The body or stdout.</param>
    /// <param name="goldenText">The golden file's text, when the expectation names one.</param>
    /// <returns>How many assertions there were and which failed (values uncapped and unscrubbed).</returns>
    public static CheckedExpectation Evaluate(int step, Expectation expect, int actualCode, string output, string? goldenText) =>
        throw new NotSupportedException();
}
