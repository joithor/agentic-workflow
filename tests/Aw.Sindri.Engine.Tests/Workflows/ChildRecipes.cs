namespace Aw.Sindri.Tests.Workflows;

/// <summary>The recipes the child workflow tests run (engine P7 U2): a child that waits an hour for 't.go' and then parks or escalates, and the parents that wait on it.</summary>
internal static class ChildRecipes
{
    /// <summary>The child: waits for 't.go'; after an hour its timeout <paramref name="then"/> parks or escalates it.</summary>
    public static string Child(string then = "park") => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-child",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: waiting",
            "states:",
            "  waiting: { kind: wait, waitFor: { event: t.go }, timeout: { after: 1h, then: " + then + " }, next: \"Waiting for {item}.\" }",
            "  done:    { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: waiting, to: done, on: wait.satisfied }",
            "evals: []",
        ]);

    /// <summary>The parent: one child wait; a resolved child is done, a cancelled one dropped.</summary>
    public static string Parent() => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-parent",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: run",
            "states:",
            "  run: { kind: wait, timeout: { after: 2d, then: park },",
            "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
            "         next: \"Waiting for {child}.\" }",
            "  done:    { kind: terminal, outcome: resolved }",
            "  dropped: { kind: terminal, outcome: unresolved }",
            "transitions:",
            "  - { from: run, to: done,    on: wait.satisfied, when: { outcome: resolved } }",
            "  - { from: run, to: dropped, on: wait.satisfied, when: { outcome: cancelled } }",
            "evals: []",
        ]);

    /// <summary>The looping parent: the child wait is a looped state, so it starts a new child on each entry (two rounds).</summary>
    public static string Looping() => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-parent",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: run",
            "states:",
            "  run: { kind: wait, loop: rounds, timeout: { after: 2d, then: park },",
            "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
            "         next: \"Waiting for {child}.\" }",
            "  done: { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: run, to: run,  on: wait.satisfied, when: { all: [{ outcome: resolved }, { loop: rounds, below: cap }] } }",
            "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }",
            "loops: { rounds: { cap: 2 } }",
            "evals: []",
        ]);

    /// <summary>The middle recipe of a grandchild: it waits on 'workflow.t-child' and ends when that does.</summary>
    public static string Mid() => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-mid",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: run",
            "states:",
            "  run: { kind: wait, timeout: { after: 2d, then: park },",
            "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
            "         next: \"Waiting for {child}.\" }",
            "  done: { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }",
            "evals: []",
        ]);

    /// <summary>The top recipe of a grandchild: it waits on 'workflow.t-mid'.</summary>
    public static string Top() => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-top",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: run",
            "states:",
            "  run: { kind: wait, timeout: { after: 2d, then: park },",
            "         waitFor: { child: { recipe: workflow.t-mid, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
            "         next: \"Waiting for {child}.\" }",
            "  done: { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: run, to: done, on: wait.satisfied, when: { outcome: resolved } }",
            "evals: []",
        ]);

    /// <summary>A recipe that waits for 't.go' and then moves into a child wait: before engine P7 that transition was rejected (no child recipes were pinned).</summary>
    public static string Gate() => string.Join('\n',
        [
            "schema: 1",
            "id: workflow.t-gate",
            "version: 1",
            "inputs:",
            "  - { key: fact:request, label: \"Request\", type: text, kind: text }",
            "initial: waiting",
            "states:",
            "  waiting: { kind: wait, waitFor: { event: t.go }, timeout: { after: 1d, then: park }, next: \"Waiting for {item}.\" }",
            "  run: { kind: wait, timeout: { after: 2d, then: park },",
            "         waitFor: { child: { recipe: workflow.t-child, inputs: { \"fact:request\": { fact: fact:request } }, onParked: propagate } },",
            "         next: \"Waiting for {child}.\" }",
            "  done: { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: waiting, to: run,  on: wait.satisfied }",
            "  - { from: run,     to: done, on: wait.satisfied, when: { outcome: resolved } }",
            "evals: []",
        ]);
}
