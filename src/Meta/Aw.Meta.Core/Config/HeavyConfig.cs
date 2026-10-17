namespace Aw.Meta.Core.Config;

/// <summary>How the gates share the box-wide heavy slots; every field is optional.</summary>
/// <param name="Reserve">The reservation by queue depth; null means <see cref="ReserveSetting.Default"/>.</param>
/// <param name="Cap">The box's heavy slot count as the team states it (parallel gate steps default to it); absent: 3, the wrapper's default.</param>
public sealed record HeavyConfig(ReserveSetting? Reserve = null, int? Cap = null)
{
    /// <summary>The slot count in force.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveCap => Cap is > 0 ? Cap.Value : 3;

    /// <summary>The reservation setting in force.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ReserveSetting EffectiveReserve => Reserve ?? ReserveSetting.Default;
}
