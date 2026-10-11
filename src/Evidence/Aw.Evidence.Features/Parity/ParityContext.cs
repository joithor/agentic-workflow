using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.Parity;

/// <summary>What every frame of one parity run shares.</summary>
/// <param name="Manifest">The design manifest.</param>
/// <param name="ManifestDirectory">The manifest's directory (relative design PNGs resolve against it).</param>
/// <param name="Host">The vetted host.</param>
/// <param name="RunDirectory">Where the frame images go.</param>
/// <param name="Credentials">The sign-in credentials, when the manifest signs in.</param>
internal sealed record ParityContext(DesignManifest Manifest, string ManifestDirectory, ResolvedHost Host, string RunDirectory, Credentials? Credentials)
{
    internal string DesignPath(DesignFrame frame) =>
        frame.DesignPng.StartsWith('/') ? frame.DesignPng : RunPath.Combine(ManifestDirectory, frame.DesignPng);
}
