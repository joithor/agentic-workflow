using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The SND-ONYX-011 a compose command that ran past its time limit stands for, and the way to tell it from another compose failure (a step may wait it out).</summary>
public static class ComposeFailures
{
    private const string Marker = "didn't finish within";

    /// <summary>Builds the error of a docker command that was stopped for running too long.</summary>
    /// <param name="what">The command as it reads in the message, such as compose up.</param>
    /// <param name="timeout">The limit it ran past.</param>
    /// <returns>SND-ONYX-011 saying how long it was allowed and that a rerun resumes.</returns>
    public static Error TimedOut(string what, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(what);
        var allowed = timeout.TotalMinutes >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{timeout.TotalMinutes:0.#} minutes")
            : string.Create(CultureInfo.InvariantCulture, $"{timeout.TotalSeconds:0} seconds");
        return SindriErrors.OnyxComposeFailed.With($"`docker {what}` {Marker} {allowed} and was stopped; run `sindri onyx up` again to resume");
    }

    /// <summary>Tells whether an error is a docker command that was stopped for running too long.</summary>
    /// <param name="error">Any error.</param>
    /// <returns>True for the error <see cref="TimedOut"/> builds.</returns>
    public static bool IsTimeout(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code == SindriErrors.OnyxComposeFailed.Code && error.Message.Contains(Marker, StringComparison.Ordinal);
    }
}
