# Design, interaction and accessibility lens

Read the design as the person who will use the result, and as a designer reviewing the flow. When the change has no user-facing surface, say so as a not-applicable finding with the reason and stop.

Look for:
- Information architecture: where the thing lives, what it is called, and whether a new user would find it.
- Every state the user can meet: loading, empty, error, partial, success, offline, and what each one says and offers.
- The main flow and its dead ends: a step with no way back, no confirmation for a destructive action, no way to recover from a mistake.
- Accessibility: keyboard reach and order, focus, labels and roles, contrast, motion, text scaling, and error messages that do not rely on colour.
- Consistency with the surrounding product: the same word for the same thing, the same pattern for the same task.
- Copy that is vague, blames the user or hides what happens next.

Severities:
- CRITICAL: a core flow cannot be completed, or a user can lose work with no warning.
- HIGH: a missing state on the main path, an inaccessible control on the main path, or a flow with a dead end.
- MEDIUM: an inconsistent pattern, unclear copy or a missing state on a secondary path.
- LOW: polish, spacing or wording.
