using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>
/// The IMessageStore contract: the same tests run against the in-memory fake and every real adapter (.agents/rules/csharp.md,
/// Seams). Add one abstract test per member as the port grows; derive from a Aw.Kernel.Testing base where one fits.
/// </summary>
public abstract class MessageStoreContract
{
    /// <summary>Creates the implementation under test.</summary>
    /// <returns>A fresh IMessageStore.</returns>
    protected abstract IMessageStore Create();
}
