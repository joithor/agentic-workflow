namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>The fix a fact key written in the wrong case gets (fact names are camelCase).</summary>
public static class FactKeyHint
{
    /// <summary>The fix for <paramref name="text"/>, which isn't a fact key.</summary>
    /// <param name="text">What the recipe wrote.</param>
    /// <returns>"fact names are camelCase: write fact:evidenceSummary" when the text is a fact key but for its case or hyphens; otherwise "write fact:&lt;camelCaseName&gt;".</returns>
    public static string Fix(string text) => throw new NotSupportedException("FactKeyHint.Fix is not implemented yet");
}
