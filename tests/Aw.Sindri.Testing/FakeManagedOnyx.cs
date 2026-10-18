using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>What the approved profile says about Onyx, set by the test.</summary>
public sealed class FakeManagedOnyx : IManagedOnyx
{
    /// <summary>Gets or sets a value indicating whether the profile names a managed Onyx.</summary>
    public bool Managed { get; set; }

    /// <summary>Gets or sets a value indicating whether the profile names an Onyx of any kind.</summary>
    public bool Configured { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<bool>> Is(CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(Managed);

    /// <inheritdoc />
    public ValueTask<Result<bool>> IsConfigured(CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(Configured || Managed);
}
