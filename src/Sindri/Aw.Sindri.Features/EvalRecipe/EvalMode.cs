namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>What 'recipe eval' does (spec §6.3).</summary>
public enum EvalMode
{
    /// <summary>'recipe eval &lt;file&gt;': run the recipe's eval cases.</summary>
    Run = 1,

    /// <summary>'recipe eval --scaffold &lt;file&gt;': print a happy-path case for the recipe.</summary>
    Scaffold = 2,
}
