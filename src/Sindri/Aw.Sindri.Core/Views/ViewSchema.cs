namespace Aw.Sindri.Core.Views;

/// <summary>The view schema version: bump it when a projection's fold changes, and the projector rebuilds.</summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (2: API P3's item views).</summary>
    public const int Version = 2;
}
