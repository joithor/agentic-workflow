using Aw.Kernel;
using Aw.Sindri.Core.Auth;

namespace Aw.Sindri.Infrastructure.Auth;

/// <summary>The per-start human token as &lt;home&gt;/auth/human.token (0600 in a 0700 folder), API spec §6.</summary>
/// <param name="settings">The settings naming the token file.</param>
internal sealed class FileHumanToken(SindriSettings settings) : IHumanTokenStore
{
    public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: mint {settings.TokenPath}");

    public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: read {settings.TokenPath}");

    public ValueTask<Result<Unit>> Remove(CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: remove {settings.TokenPath}");
}
