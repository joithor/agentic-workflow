namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Onyx's BaseFilters (onyx/context/search/models.py:104), sent empty: Sindri wants recall over every connector the
/// key can see, and Onyx's only time filter keeps documents updated since a time, which isn't an as-of cut.
/// </summary>
internal sealed record OnyxSearchFilters;
