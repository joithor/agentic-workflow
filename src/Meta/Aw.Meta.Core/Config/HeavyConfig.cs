namespace Aw.Meta.Core.Config;

/// <summary>How the gates share the box-wide heavy slots; every field is optional.</summary>
/// <param name="Reserve">The reservation by queue depth; null means <see cref="ReserveSetting.Default"/>.</param>
public sealed record HeavyConfig(ReserveSetting? Reserve = null)
{
    /// <summary>The reservation setting in force.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ReserveSetting EffectiveReserve => Reserve ?? ReserveSetting.Default;
}
