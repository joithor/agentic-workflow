namespace Aw.Sindri.Core.Views;

/// <summary>The view schema version: bump it when a projection's fold changes, and the projector rebuilds.</summary>
public static class ViewSchema
{
    /// <summary>The version this build folds with (4: API P11's engine item states; 5 once API P6's view_jobs lands first).</summary>
    public const int Version = 4;
}
