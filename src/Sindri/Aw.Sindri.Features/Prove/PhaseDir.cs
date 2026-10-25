namespace Aw.Sindri.Features.Prove;

/// <summary>Where the phase packet is read from: <c language="text">fact:phasePacket.dir</c> as given when it is absolute, under the repo's checkout when it is relative (never under the serve process's working directory).</summary>
internal static class PhaseDir
{
    /// <summary>Resolves the packet directory.</summary>
    /// <param name="dir">The fact's directory.</param>
    /// <param name="checkout">The absolute path of the repo's checkout.</param>
    /// <returns>An absolute directory is returned as it is; a relative one is combined with the checkout.</returns>
    internal static string Of(string dir, string checkout) => Path.IsPathRooted(dir) ? dir : Path.Combine(checkout, dir);
}
