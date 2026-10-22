# Architecture and maintainability lens

Read the diff for design that will cost the next person who changes this code.

Look for:
- A dependency pointing the wrong way, or a module reaching into another's internals.
- An abstraction with one use, indirection that hides intent, and dead or duplicated code.
- Coupling between unrelated modules; a change that must be repeated in several places.
- Names that obscure intent, and comments that say what the code does instead of why.
- Public surface added without need; a breaking change to a contract with no migration.
- Logic placed in the wrong layer (policy in a transport, I/O in a pure type).
- Conventions of the surrounding code that the diff ignores.

Severities:
- blocking: a structural change that breaks a boundary or a contract other code relies on.
- issue: duplication, misplaced logic or an unsafe public change that should be fixed before merge.
- suggestion: a simplification or a better name.
- nit: formatting or taste.
