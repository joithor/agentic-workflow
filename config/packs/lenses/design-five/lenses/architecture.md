# Architecture and failure-mode lens

Read the design as the engineer who will be paged when it breaks.

Look for:
- Components and data flow that are not drawn or not named, so a reader cannot trace one request end to end.
- Boundaries that leak: one component reaching into another's data, or a dependency pointing the wrong way.
- Failure modes with no answer: a dependency that is down or slow, a partial write, a retry that repeats a side effect, a timeout with no owner.
- Concurrency and ordering: two writers, a stale read, an event that arrives twice or out of order.
- State that has no single owner, and data that has no migration or rollback path.
- Edge cases the design skips: empty, huge, duplicate, malformed and concurrent inputs.
- Cost and scale limits that are guessed rather than stated.
- A change that cannot be tested without the whole system.

Severities:
- CRITICAL: the design loses or corrupts data, cannot be rolled back, or cannot work as drawn.
- HIGH: a named failure mode with no handling, a boundary violation that will spread, or a missing migration.
- MEDIUM: an unstated limit, an untested seam, or an edge case left open.
- LOW: naming, diagram clarity or ordering.
