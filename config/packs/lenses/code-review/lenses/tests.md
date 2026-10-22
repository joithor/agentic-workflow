# Tests lens

Read the diff for whether its behaviour is proven by tests that would fail if it broke.

Look for:
- A changed behaviour with no test, and a bug fix with no test that fails without the fix.
- Assertions that cannot fail, check only that code ran, or repeat the implementation.
- Tests coupled to internals, to timing, to order, or to the machine they run on.
- Missing edge cases: empty, boundary, error and concurrent paths.
- Mocks that hide the behaviour under test; fixtures that carry real data or secrets.
- Tests deleted or weakened to make a change pass.

Severities:
- blocking: a changed behaviour on a main path has no test at all, or a test was weakened to pass.
- issue: a test that cannot fail, or an error path that is untested.
- suggestion: an extra edge case or a clearer assertion.
- nit: naming or layout of a test.
