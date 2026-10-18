namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure merge of the filled inputs into one plan (plan decision 3): no I/O and no clock, so the workflow can run it.</summary>
public static class InjectionPlans
{
    /// <summary>Merges the inputs in declaration order and their sections in the order given; a later section of the same name replaces the earlier one in place and warns.</summary>
    /// <param name="filled">What the inputs returned.</param>
    /// <returns>The plan.</returns>
    public static InjectionPlan Merge(IReadOnlyList<FilledInput> filled)
    {
        ArgumentNullException.ThrowIfNull(filled);
        List<InjectionEntry> entries = [];
        List<InjectionWarning> warnings = [];
        foreach (var input in filled.OrderBy(static f => f.Index))
        {
            foreach (var section in input.Sections)
            {
                var at = entries.FindIndex(e => string.Equals(e.Section, section.Section, StringComparison.Ordinal));
                if (at < 0)
                {
                    entries.Add(section);
                    continue;
                }

                warnings.Add(new InjectionWarning(section.Section, entries[at].Input, section.Input));
                entries[at] = section with { Replaced = entries[at].Input };
            }
        }

        return new InjectionPlan(entries, warnings);
    }
}
