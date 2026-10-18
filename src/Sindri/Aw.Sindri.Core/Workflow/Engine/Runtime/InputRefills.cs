using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// One input's text for the open run: from memory, or by running the input again, in which case what it filled now must equal what the
/// run recorded (same sections, bytes and digests) before the text is kept or served. Nothing is served that the plan does not describe.
/// </summary>
public static class InputRefills
{
    /// <summary>The input's texts for <paramref name="run"/>.</summary>
    /// <param name="texts">The in-memory texts.</param>
    /// <param name="runner">Runs the input again.</param>
    /// <param name="address">The instance address.</param>
    /// <param name="state">The state whose Step runs.</param>
    /// <param name="run">The run token.</param>
    /// <param name="input">The input.</param>
    /// <param name="mismatch">The error a differing refill answers.</param>
    /// <param name="cancellationToken">Cancels the refill.</param>
    /// <returns>The texts; the <paramref name="mismatch"/> error when the refill differs from the record.</returns>
    public static async ValueTask<Result<FilledTexts>> Texts(IInjectionTexts texts, IStepInputRunner runner, string address, string state, string run, InjectionInputSource input, ErrorDefinition mismatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(mismatch);
        var key = new InjectionKey(address, state, run, input.Recorded.Input);
        if (texts.Get(key) is { } kept)
        {
            return kept;
        }

        var again = await runner.Fill(input.Context, input.Recorded.Index, cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return again.Error;
        }

        if (!Same(input.Recorded, again.Value.Metadata))
        {
            return mismatch.With($"input {input.Recorded.Input.Value} changed since run {run} recorded it (fix: re-run the Step)");
        }

        texts.Put(key, again.Value.Texts);
        return again.Value.Texts;
    }

    private static bool Same(FilledInput recorded, FilledInput now) =>
        recorded.Sections.Count == now.Sections.Count
        && recorded.Sections.Zip(now.Sections).All(static p => string.Equals(p.First.Section, p.Second.Section, StringComparison.Ordinal) && p.First.Bytes == p.Second.Bytes && string.Equals(p.First.Sha256, p.Second.Sha256, StringComparison.Ordinal))
        && Equals(recorded.Result, now.Result);
}
