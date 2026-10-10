using Aw.Packets.Core;

namespace Aw.Packets.Testing;

/// <summary>
/// The IFreezeLog contract: the same tests run against the in-memory fake and every real adapter (.agents/rules/csharp.md,
/// Seams). Add one abstract test per member as the port grows; derive from a Aw.Kernel.Testing base where one fits.
/// </summary>
public abstract class FreezeLogContract
{
    /// <summary>Creates the implementation under test.</summary>
    /// <returns>A fresh IFreezeLog.</returns>
    protected abstract IFreezeLog Create();
}
