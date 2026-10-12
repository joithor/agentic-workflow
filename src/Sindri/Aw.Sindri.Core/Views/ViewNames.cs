namespace Aw.Sindri.Core.Views;

/// <summary>The view tables (a fixed set: an adapter never builds a table name from input).</summary>
public static class ViewNames
{
    /// <summary>Each service's last run.</summary>
    public const string Services = "services";

    /// <summary>The approved and live profile.</summary>
    public const string Profile = "profile";

    /// <summary>Every view.</summary>
    public static IReadOnlyList<string> All { get; } = [Services, Profile];
}
