using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Records the section it was asked to write; reports a change only when it differs from the last one.</summary>
public sealed class FakeOnyxProfileWriter : IOnyxProfileWriter
{
    /// <summary>Gets the last section written, or null.</summary>
    public (string Url, string Token, string Version)? Section { get; private set; }

    /// <summary>Gets or sets an error SetOnyx returns instead of writing.</summary>
    public Error? Fails { get; set; }

    /// <inheritdoc />
    public Result<bool> SetOnyx(string url, string token, string version)
    {
        if (Fails is { } error)
        {
            return error;
        }

        var changed = Section != (url, token, version);
        Section = (url, token, version);
        return changed;
    }
}
