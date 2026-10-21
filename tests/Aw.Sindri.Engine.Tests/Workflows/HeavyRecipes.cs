namespace Aw.Sindri.Tests.Workflows;

/// <summary>The recipe the heavy-slot tests run (LN-WF3): a heavy wait, two block Steps, a desk Step, and a block Step again.</summary>
internal static class HeavyRecipes
{
    public const string Id = "workflow.t-heavy";

    /// <summary>
    /// 'slot' waits for the heavy resource; 'first' and 'second' run the judge block; 'desk' is a desk Step (no runtime), which gives the slot up;
    /// 'third' runs the block again, so it takes a slot before it starts. A failed block run moves on to the next Step.
    /// </summary>
    public static string Heavy() => string.Join('\n',
        [
            "schema: 1",
            "id: " + Id,
            "version: 1",
            "inputs:",
            "  - { key: fact:change, label: \"Change\", type: text, kind: text }",
            "  - { key: fact:diff, label: \"Diff\", type: link, kind: link }",
            "initial: slot",
            "states:",
            "  slot:   { kind: wait, waitFor: { resource: heavy }, timeout: { after: 1d, then: park }, next: \"Waiting for a heavy-job slot for {item}.\" }",
            "  first:  { kind: active, step: { use: step.judge.choose, with: { prompt: \"Which?\", facts: [fact:change, fact:diff], choices: [\"ui: a\", \"api: b\"] } }, timeout: { after: 2d, then: park }, next: \"Judge {item}.\" }",
            "  second: { kind: active, step: { use: step.judge.choose, with: { prompt: \"Which?\", facts: [fact:change, fact:diff], choices: [\"ui: a\", \"api: b\"] } }, timeout: { after: 2d, then: park }, next: \"Judge {item} again.\" }",
            "  desk:   { kind: active, step: step.produce, timeout: { after: 1d, then: park }, next: \"Do the desk work for {item}, then complete this Step.\" }",
            "  third:  { kind: active, step: { use: step.judge.choose, with: { prompt: \"Which?\", facts: [fact:change, fact:diff], choices: [\"ui: a\", \"api: b\"] } }, timeout: { after: 2d, then: park }, next: \"Judge {item} a last time.\" }",
            "  done:   { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: slot, to: first, on: wait.satisfied }",
            "  - { from: first, to: second, on: step.completed, when: { outcome: ui }, produces: [fact:decision] }",
            "  - { from: first, to: second, on: step.failed }",
            "  - { from: second, to: desk, on: step.completed, when: { outcome: ui }, produces: [fact:decision] }",
            "  - { from: second, to: desk, on: step.failed }",
            "  - { from: desk, to: third, on: step.completed, when: { outcome: done }, produces: [fact:output] }",
            "  - { from: third, to: done, on: step.completed, when: { outcome: ui }, produces: [fact:decision] }",
            "  - { from: third, to: done, on: step.failed }",
            "evals: []",
        ]);
}
