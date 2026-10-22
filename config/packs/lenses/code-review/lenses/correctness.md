# Correctness lens

Read the diff for logic that does not do what it says. Report only what the diff changes or newly depends on.

Look for:
- Logic errors, inverted conditions, off-by-one, wrong operator, wrong variable.
- Edge cases: empty, null, zero, one, many, duplicate, very large, malformed input.
- State that is read before it is set, set twice, or left half-updated when a step fails.
- Errors that are swallowed, mapped to the wrong result, or reported without their cause.
- A changed contract (argument, return value, ordering, default) with a caller that was not updated.
- A change whose code does not match its stated intent.

Severities:
- blocking: the behaviour is wrong, or data is lost or corrupted. Mark `behaviour: "wrong"` when the change does not do what was asked.
- issue: a defect on a real path that must be fixed before merge.
- suggestion: a safer or clearer way to do the same thing.
- nit: naming, formatting or taste.
