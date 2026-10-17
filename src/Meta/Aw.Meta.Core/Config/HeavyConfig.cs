namespace Aw.Meta.Core.Config;

/// <summary>How the gates share the box-wide heavy slots; every field is optional.</summary>
/// <param name="Reserve">The reservation by queue depth; null means <see cref="ReserveSetting.Default"/>.</param>
/// <param name="Cap">The box-wide heavy slot count the reservation is sized against (the wrapper's own cap is set elsewhere; keep them equal).</param>
public sealed record HeavyConfig(ReserveSetting? Reserve = null, int Cap = HeavyConfig.DefaultCap)
{
    /// <summary>The default slot count.</summary>
    public const int DefaultCap = 3;

    /// <summary>The reservation setting in force.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ReserveSetting EffectiveReserve => Reserve ?? ReserveSetting.Default;
}
