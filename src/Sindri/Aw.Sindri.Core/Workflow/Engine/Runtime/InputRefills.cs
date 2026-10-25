using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// One input's text for the open run: from memory, or by running the input again, in which case what it filled now must equal what the
/// run recorded (same sections, bytes and digests) before the text is kept or served. Nothing is served that the plan does not describe.
/// </summary>
public static class InputRefills
{
    /// <summary>The input's texts for the run.</summary>
    /// <param name="texts">The in-memory texts.</param>
    /// <param name="runner">Runs the input again.</param>
    /// <param name="refill">Which input of which run, and the error a differing refill answers.</param>
    /// <param name="cancellationToken">Cancels the refill.</param>
    /// <returns>The texts; the mismatch error when the refill differs from the record.</returns>
    public static async ValueTask<Result<FilledTexts>> Texts(IInjectionTexts texts, IStepInputRunner runner, InputRefill refill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(refill);
        var input = refill.Input;
        var key = new InjectionKey(refill.Address, refill.State, refill.Run, input.Recorded.Index);
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
            return refill.Mismatch.With($"input {input.Recorded.Input.Value} changed since run {refill.Run} recorded it (fix: re-run the Step)");
        }

        texts.Put(key, again.Value.Texts);
        return again.Value.Texts;
    }

    private static bool Same(FilledInput recorded, FilledInput now) =>
        recorded.Sections.Count == now.Sections.Count
        && recorded.Sections.Zip(now.Sections).All(static p => string.Equals(p.First.Section, p.Second.Section, StringComparison.Ordinal) && p.First.Bytes == p.Second.Bytes && string.Equals(p.First.Sha256, p.Second.Sha256, StringComparison.Ordinal))
        && Equals(recorded.Result, now.Result);
}
