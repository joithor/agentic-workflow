namespace Aw.Sindri.Core.Auth;

/// <summary>The human token this process minted, if any: held from start to stop, like HeldLease holds the grant.</summary>
public sealed class HeldHumanToken
{
    private HumanToken? _token;

    /// <summary>The current token, or null.</summary>
    public HumanToken? Current => Volatile.Read(ref _token);

    /// <summary>Holds <paramref name="token"/>.</summary>
    /// <param name="token">The minted token.</param>
    public void Hold(HumanToken token) => Volatile.Write(ref _token, token);

    /// <summary>Forgets the token (on stop).</summary>
    public void Clear() => Volatile.Write(ref _token, null);
}
