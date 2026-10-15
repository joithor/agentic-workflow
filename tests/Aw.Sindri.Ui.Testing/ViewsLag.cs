namespace Aw.Sindri.Ui.Testing;

/// <summary>How the fake's views answer a read that carries X-Sindri-After.</summary>
internal enum ViewsLag
{
    /// <summary>The views are current: the read answers.</summary>
    None,

    /// <summary>The projection is stalled: SND-API-011 at once.</summary>
    Stalled,

    /// <summary>The projection is catching up: SND-API-011 after the server's 2 s wait.</summary>
    Behind,
}
