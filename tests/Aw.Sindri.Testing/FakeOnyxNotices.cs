using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>The managed Onyx's notice, set by the test (null: nothing needs the human).</summary>
public sealed class FakeOnyxNotices : IOnyxNotices
{
    /// <summary>Gets or sets the notice.</summary>
    public OnyxNotice? Notice { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<OnyxNotice?>> Current(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Notice));
}
