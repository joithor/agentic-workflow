# Interface timing and races lens

Read the diff for asynchronous interface code that behaves differently depending on timing.

Look for:
- A response that arrives after the view has changed or closed, and overwrites newer state.
- Two requests in flight for the same thing with no cancellation or ordering.
- Effects, listeners and timers that are not cleaned up; subscriptions that leak.
- State read inside a callback after it has gone stale; double submission of a form or action.
- Rendering that flickers, jumps or shows a state that was never valid.

Severities:
- blocking: a race that loses or duplicates a user's action.
- issue: stale or leaked state on a common path.
- suggestion: a cancellation, debounce or guard that makes the code safer.
- nit: naming or ordering of effects.
