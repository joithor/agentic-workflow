namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The subject kinds of spec §3.3 and the fact type each pairs with (RCP-TYPE-002, RCP-TYPE-003).</summary>
public static class SubjectKinds
{
    /// <summary>The five subject kinds.</summary>
    public static IReadOnlyList<string> All { get; } = ["item", "linear-project", "webhook", "file", "text"];

    /// <summary>Whether <paramref name="kind"/> is one of <see cref="All"/>.</summary>
    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);

    /// <summary>The fact type a kind pairs with ('item' with item, 'file' with file, 'text' with text, the other two with payload), or null for an unknown kind.</summary>
    public static string? PairedType(string kind) => kind switch
    {
        "item" => "item",
        "file" => "file",
        "text" => "text",
        "linear-project" or "webhook" => "payload",
        _ => null,
    };
}
