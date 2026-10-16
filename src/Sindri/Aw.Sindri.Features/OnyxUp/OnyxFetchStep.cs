using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 2: the pinned deployment files, fetched from the pinned commit and verified against the lock.
internal sealed class OnyxFetchStep(IDeployFiles files, IOnyxWorkspace workspace, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Fetch;

    public string Stamp(OnyxUpContext context) => OnyxSteps.Stamp([.. context.Pins.Files.Select(static f => f.Path + " " + f.Sha256)]);

    public ValueTask<bool> IsDone(OnyxUpContext context, string? recorded, CancellationToken cancellationToken)
        => ValueTask.FromResult(string.Equals(recorded, Stamp(context), StringComparison.Ordinal) && workspace.Read(context.Paths.Compose) is not null);

    public async ValueTask<Result<Unit>> Run(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var fetched = await files.Fetch(context.Pins, context.Paths.DeployDir, progress.Line, cancellationToken).ConfigureAwait(false);
        return fetched.IsError ? fetched.Error : Results.Ok();
    }
}
