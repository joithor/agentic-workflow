using System.Globalization;

namespace Aw.Sindri.Tests.ManifestRecipes;

/// <summary>Small valid recipes (and eval cases) for the manifest, shipped-eval and --against tests.</summary>
internal static class SampleRecipes
{
    /// <summary>A one-step recipe that declares <paramref name="evals"/> and a display name.</summary>
    internal static string Recipe(string name, int version = 1, string? displayName = "Produce and finish", params string[] evals) =>
        "schema: 1\n"
        + $"id: workflow.{name}\n"
        + $"version: {version.ToString(CultureInfo.InvariantCulture)}\n"
        + (displayName is null ? string.Empty : $"displayName: {displayName}\n")
        + "inputs: [{ key: fact:spec, label: \"Spec\", type: text, kind: text }]\n"
        + (evals.Length == 0 ? string.Empty : $"evals: [{string.Join(", ", evals)}]\n")
        + "initial: produce\n"
        + "states:\n"
        + "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the output.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }\n";

    /// <summary>An eval case that reaches 'done'; <paramref name="expected"/> is the state the single trigger is expected to reach.</summary>
    internal static string Case(string expected = "done") =>
        "{\"start\":{\"inputs\":{\"fact:spec\":\"Add a flag.\"}}}\n"
        + "{\"trigger\":{\"kind\":\"step.completed\",\"outcome\":\"done\",\"outputs\":{\"fact:output\":\"Added it.\"}},\"expect\":{\"state\":\"" + expected + "\"" + (expected == "done" ? ",\"outcome\":\"resolved\"" : string.Empty) + "}}\n";

    /// <summary>The path an eval case of recipe <paramref name="name"/> is listed under.</summary>
    internal static string EvalPath(string name, string caseName = "happy") => $"evals/workflow.{name}/{caseName}.jsonl";
}
