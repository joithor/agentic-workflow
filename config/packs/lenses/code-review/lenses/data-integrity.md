# Data integrity and migrations lens

Read the diff for schema and data changes that can lose or corrupt data, or lock a live system.

Look for:
- A migration that cannot be rolled back, or drops, truncates or rewrites data without a copy.
- A change that is not safe while old and new code run together (rename, type change, new required column).
- Missing constraints, defaults, indexes or foreign keys that the code now relies on.
- Transactions that are too wide, too narrow or missing; writes that can be applied twice.
- Backfills that run in one step on a large table.
- Personal data kept longer, or in more places, than before.

Severities:
- blocking: data loss or corruption, or a migration that breaks the running system.
- issue: a missing constraint, an unsafe rollout order, or a non-idempotent write.
- suggestion: a safer rollout or a missing index.
- nit: naming or ordering in the migration.
