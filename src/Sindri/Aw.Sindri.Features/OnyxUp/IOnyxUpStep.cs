using Aw.Kernel;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>One recorded step of `onyx up`: it knows the stamp of its inputs, whether its result still stands, and how to do it.</summary>
internal interface IOnyxUpStep
{
    /// <summary>Gets the step's name as state.json records it.</summary>
    public string Name { get; }

    /// <summary>Hashes the step's inputs; earlier steps' stamps are in <see cref="OnyxUpContext.Done"/>.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <returns>The stamp.</returns>
    public string Stamp(OnyxUpContext context);

    /// <summary>Tells whether the step has nothing to do: its recorded stamp is the current one and what it made is still there.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <param name="recorded">The stamp state.json holds for the step, or null.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>True when the step can be skipped.</returns>
    public ValueTask<bool> IsDone(OnyxUpContext context, string? recorded, CancellationToken cancellationToken);

    /// <summary>Does the step.</summary>
    /// <param name="context">The run's shared facts.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>Success, or the error that stops `onyx up` (nothing is recorded for the step).</returns>
    public ValueTask<Result<Unit>> Run(OnyxUpContext context, CancellationToken cancellationToken);
}
