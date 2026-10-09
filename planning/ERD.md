# Entity-Relationship Diagram -- MCP Bridge

## Relationships

```mermaid
erDiagram
    messages {
        TEXT id PK "UUIDv4"
        TEXT conversation "NOT NULL, UUIDv4"
        TEXT sender "NOT NULL"
        TEXT recipient "NOT NULL"
        TEXT kind "NOT NULL, CHECK"
        TEXT payload "NOT NULL"
        TEXT meta_prompt "NULLABLE"
        TEXT created_at "NOT NULL, DEFAULT datetime('now')"
        TEXT read_at "NULLABLE"
    }

    tasks {
        TEXT id PK "UUIDv4"
        TEXT conversation "NOT NULL, UUIDv4"
        TEXT domain "NOT NULL"
        TEXT summary "NOT NULL"
        TEXT details "NOT NULL"
        TEXT analysis "NULLABLE"
        TEXT assigned_to "NULLABLE"
        TEXT status "NOT NULL, DEFAULT 'pending', CHECK"
        TEXT created_at "NOT NULL, DEFAULT datetime('now')"
        TEXT updated_at "NOT NULL, DEFAULT datetime('now')"
    }

    tasks ||--o{ messages : "assign_task creates a 'task' message; report_status creates a 'status' message"
```

There are no foreign-key constraints between `messages` and `tasks`. The relationship is enforced at the application layer:

- **assign_task** inserts one `tasks` row and one `messages` row (kind = `'task'`, payload contains `task_id`) inside a single transaction.
- **report_status** inserts one `messages` row (kind = `'status'`) and optionally updates the referenced `tasks` row (by `task_id`) inside a single transaction.
- Both tables share a `conversation` UUID that acts as a logical grouping key, but there is no FK constraint enforcing referential integrity.

---

### `messages`

Stores all inter-agent messages: context pushes, task notifications, status reports, and replies.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `id` | TEXT (UUIDv4) | NOT NULL | Primary key. Generated server-side via `crypto.randomUUID()`. |
| `conversation` | TEXT (UUIDv4) | NOT NULL | Groups messages into a logical conversation thread. |
| `sender` | TEXT | NOT NULL | Identifier of the sending agent (e.g. `"claude-code"`, `"codex"`). |
| `recipient` | TEXT | NOT NULL | Identifier of the receiving agent. |
| `kind` | TEXT | NOT NULL | Message type. **CHECK constraint:** must be one of `'context'`, `'task'`, `'status'`, `'reply'`. |
| `payload` | TEXT | NOT NULL | Free-form content. For kind `'task'`, this is a JSON object containing `task_id`, `domain`, `summary`, `details`. |
| `meta_prompt` | TEXT | Yes | Optional guidance for how the recipient should process the message. Only set on `'context'` messages. |
| `created_at` | TEXT (ISO 8601) | NOT NULL | Insertion timestamp. **DEFAULT:** `datetime('now')`. |
| `read_at` | TEXT (ISO 8601) | Yes | Set when the message is marked as read. `NULL` means unread. |

**Indexes:**

| Index Name | Columns | Purpose |
|---|---|---|
| `idx_messages_conversation` | `conversation` | Fast lookup of all messages in a conversation. |
| `idx_messages_recipient` | `recipient, read_at` | Fast lookup of unread messages for a given recipient (`WHERE recipient = ? AND read_at IS NULL`). |

**CHECK constraints:**

| Column | Constraint |
|---|---|
| `kind` | `kind IN ('context', 'task', 'status', 'reply')` |

---

### `tasks`

Stores task assignments with domain classification, implementation details, and lifecycle status.

| Field | Type | Nullable | Notes |
|---|---|---|---|
| `id` | TEXT (UUIDv4) | NOT NULL | Primary key. Generated server-side via `crypto.randomUUID()`. |
| `conversation` | TEXT (UUIDv4) | NOT NULL | Links this task to a conversation thread. |
| `domain` | TEXT | NOT NULL | Task domain classifier (e.g. `"frontend"`, `"backend"`, `"security"`). |
| `summary` | TEXT | NOT NULL | Brief one-line task summary. |
| `details` | TEXT | NOT NULL | Full implementation instructions. |
| `analysis` | TEXT | Yes | Analysis or research notes. Can be set at creation or updated via `report_status`. On status update, only overwritten if a new value is provided (`COALESCE(@analysis, analysis)`). |
| `assigned_to` | TEXT | Yes | Agent identifier the task is assigned to. `NULL` means unassigned. |
| `status` | TEXT | NOT NULL | Lifecycle state. **DEFAULT:** `'pending'`. **CHECK constraint:** must be one of `'pending'`, `'in_progress'`, `'completed'`, `'failed'`. |
| `created_at` | TEXT (ISO 8601) | NOT NULL | Insertion timestamp. **DEFAULT:** `datetime('now')`. |
| `updated_at` | TEXT (ISO 8601) | NOT NULL | Last-modified timestamp. **DEFAULT:** `datetime('now')`. Updated on every `updateTaskStatus` call. |

**Indexes:**

| Index Name | Columns | Purpose |
|---|---|---|
| `idx_tasks_conversation` | `conversation` | Fast lookup of all tasks in a conversation. |
| `idx_tasks_status` | `status` | Fast filtering by task lifecycle state. |

**CHECK constraints:**

| Column | Constraint |
|---|---|
| `status` | `status IN ('pending', 'in_progress', 'completed', 'failed')` |

---

---

### Derived View: ConversationSummary

Not a physical table — produced by `getConversations(limit, offset)` in `db/client.ts` using a `UNION ALL` aggregation query over both tables:

| Field | Source | Description |
|---|---|---|
| `conversation` | messages / tasks | UUID grouping key |
| `participants` | messages.sender + messages.recipient | Unique agent identifiers in the conversation |
| `message_count` | COUNT of messages rows | Total message volume |
| `task_count` | COUNT of tasks rows | Total task volume |
| `last_activity` | MAX(created_at) across both tables | Most recent event timestamp |

Results are ordered by `last_activity DESC` and support `limit` / `offset` pagination.

---

## Database Configuration

- **Engine:** SQLite via `better-sqlite3`
- **Journal mode:** WAL (`PRAGMA journal_mode = WAL`)
- **Foreign keys:** Enabled (`PRAGMA foreign_keys = ON`), though no FK constraints are currently defined
- **Default path:** `bridge.db` in the process working directory

## Sindri ledger

`$AW_STATE_DIR/sindri/ledger.db` (SQLite, WAL, file 0600, dir 0700). Sindri is the only writer; hooks append to spool files instead (spec §5.2). The schema version is `PRAGMA user_version`; migrations live in `sindri/src/ledger/db.ts` and are append-only, run in one `IMMEDIATE` transaction, and copy the file to `ledger.db.bak-v<old>` first. A ledger newer than the running sindri is refused with `SND-LEDGER-001`.

```mermaid
erDiagram
    meta {
        TEXT key PK "epoch"
        TEXT value "NOT NULL"
    }
    items {
        TEXT source PK "NOT NULL, <tracker type>:<repo>, e.g. plan-file:<repo>"
        TEXT id PK "tracker item id, unique only within its source"
        TEXT title "NOT NULL, scrubbed, <=200 chars"
        TEXT state "open | done | removed"
        TEXT size "XS..XL, NULLABLE"
        TEXT sized_by "rules | judge, NULLABLE"
        TEXT ambiguity "none | unknown, NULLABLE"
        INTEGER steps_done
        INTEGER steps_total
        TEXT content_hash "NOT NULL"
        TEXT first_seen "ISO-8601"
        TEXT last_seen "ISO-8601"
        INTEGER epoch "fencing epoch of the last write"
    }
    item_events {
        INTEGER seq PK
        TEXT source FK "with item_id, references items(source, id)"
        TEXT item_id FK
        TEXT ts "ISO-8601"
        TEXT kind "seen | changed | state-changed | removed"
        TEXT detail "scrubbed JSON, <=2000 chars"
        INTEGER epoch
        TEXT tick_id "ulid of the run"
    }
    profile_approvals {
        TEXT hash PK "sha256 of the approved bytes; the row inserted last (highest rowid) is the latest approval"
        TEXT approved_at "ISO-8601"
        TEXT approved_by "OS user"
    }
    cursors {
        TEXT source PK "adapter:repo"
        TEXT cursor
        TEXT updated_at
    }
    shape_runs {
        TEXT run_id PK "ulid"
        TEXT repo "NOT NULL"
        TEXT ts "ISO-8601"
        TEXT head "HEAD at record time, NULLABLE"
        TEXT tree "git write-tree of the staged index, NULLABLE"
        TEXT commit_sha "set by reconcile once a commit with that tree is found, NULLABLE"
        INTEGER elapsed_ms
        INTEGER index_age_ms "NULLABLE"
        TEXT providers "JSON: layer status at record time"
        TEXT parser "parse-ts@<INDEXER_VERSION>+ts<typescript version> that computed the AST hashes"
        TEXT deferred "JSON: checks skipped over the commit budget"
        INTEGER signal_count
        INTEGER epoch
        TEXT closed_at "set when a run is still unlinked at shape.outcomeDays: never links later, NULLABLE"
    }
    shape_signals {
        INTEGER seq PK
        TEXT run_id FK "references shape_runs(run_id)"
        TEXT type "e.g. reinvented:exact"
        TEXT layer
        REAL value
        REAL threshold
        TEXT at "file:line"
        TEXT existing "the matching existing symbol, NULLABLE"
        TEXT detail "scrubbed, names fenced as untrusted"
        TEXT name "flagged symbol or dependency, NULLABLE"
        TEXT ast_hash "flagged symbol hash, NULLABLE"
        TEXT outcome "kept | acted-on | dropped | n/a, NULLABLE until labeled"
        TEXT labeled_at "ISO-8601, NULLABLE"
        INTEGER epoch
    }
    scope_runs {
        TEXT run_id PK "ulid"
        TEXT subject "NOT NULL, the brief file or linear:<project>"
        TEXT mode "NOT NULL, scope | backtest"
        TEXT ts "NOT NULL, ISO-8601"
        TEXT status "NOT NULL, e.g. complete | incomplete"
        INTEGER rounds "NOT NULL"
        INTEGER surfaces "NOT NULL"
        REAL recall "NULLABLE, backtest only"
        REAL precision "NULLABLE, backtest only"
        REAL baseline_recall "NULLABLE"
        REAL baseline_precision "NULLABLE"
        INTEGER leaky "NOT NULL, 1 when the code index was used in a backtest"
        INTEGER tokens "NOT NULL"
        TEXT out_path "NOT NULL"
        INTEGER epoch "NOT NULL"
    }
    model_calls {
        TEXT run_id PK "correlation id, no foreign key (any Step's run)"
        TEXT step "NOT NULL DEFAULT 'scope': scope | backtest, later triage, reflect, ..."
        INTEGER seq PK "with run_id"
        TEXT role "NOT NULL, e.g. scoping | challenger | adjudicator"
        TEXT model "NOT NULL"
        INTEGER input_tokens "NOT NULL"
        INTEGER output_tokens "NOT NULL"
    }
    items ||--o{ item_events : "has"
    shape_runs ||--o{ shape_signals : "records"
    scope_runs ||..o{ model_calls : "audits (by run_id, not enforced)"
```

Items are keyed by `(source, id)`: an item id is unique only within its tracker source, so two repos with a same-named plan file never share a row, and `markMissing` closes only its own source's items. `item_events` references that composite key.

Every write runs inside `withEpoch(db, epoch, …)` or `fenced(db, epoch, …)`, an `IMMEDIATE` transaction that rejects a stale epoch (spec §9.1): `withEpoch` throws `SND-LOCK-003`, and `fenced` returns `{ ok: false }` so `observe` can stop as a no-op. Re-approving a profile deletes and re-inserts its `profile_approvals` row, so a rollback becomes the latest approval.

Ledger v2 adds `shape_runs` and `shape_signals` (record-only shape signals; outcomes are labeled by tree reconcile once a commit is `shape.outcomeDays` old). The v1 to v2 migration keeps all rows and leaves `ledger.db.bak-v1`. Indexes: `shape_runs_pending (commit_sha, closed_at, ts)` for the runs waiting for a commit, `shape_runs_tree (repo, tree, parser, ts, run_id)` for the one-run-per-staged-tree rule, and `shape_signals_type`, `shape_signals_run`.

Ledger v3 adds `scope_runs` (one row per `sindri scope` run or backtest) and `model_calls` (one row per model call, keyed by `(run_id, seq)`, written in the same transaction as the run's row). `model_calls` audits every Step, not only scoping: `run_id` is a plain correlation id with no foreign key to `scope_runs` (a later Step such as triage or reflect has no `scope_runs` parent), and `step` names the Step that made the call (`scope` or `backtest` today; the column defaults to `scope`). The v2 to v3 migration keeps all rows and leaves `ledger.db.bak-v2`.

## Sindri code index

`$AW_STATE_DIR/sindri/index/<repo>.db`, one SQLite file per repo (schema: `sindri/src/index/db.ts`). It is derived data: a different `user_version` is rebuilt, not migrated, and a build writes a temp copy and renames it over the old file.

```mermaid
erDiagram
    meta {
        TEXT key PK
        TEXT value "NOT NULL"
    }
    layers {
        TEXT layer PK "structure | clones | deps | embeddings | graph"
        TEXT stamp "version stamp: indexer, model, graphify, commit"
        TEXT status "ok | unavailable | disabled | pending"
        TEXT detail "reason when not ok"
        TEXT built_at "ISO-8601"
    }
    files {
        TEXT path PK
        TEXT hash "NOT NULL"
        INTEGER size
    }
    symbols {
        INTEGER id PK
        TEXT file FK "references files(path)"
        TEXT name
        TEXT kind
        INTEGER start_line
        INTEGER end_line
        INTEGER exported
        INTEGER utility
        TEXT signature
        TEXT ast_hash "normalized AST hash"
        INTEGER token_count
        INTEGER complexity
        TEXT callees "JSON"
        BLOB minhash
        TEXT body "scrubbed"
    }
    bands {
        TEXT key "LSH band key"
        INTEGER symbol_id FK "references symbols(id)"
    }
    deps {
        TEXT manifest PK
        TEXT name PK
        TEXT version
        TEXT kind
        TEXT tags "purpose tags, JSON"
    }
    embeddings {
        INTEGER symbol_id PK "references symbols(id)"
        TEXT model
        BLOB vector
    }
    graph_nodes {
        TEXT id PK
        TEXT file "NULLABLE"
        TEXT name "NULLABLE"
        INTEGER line "NULLABLE"
    }
    graph_edges {
        TEXT src
        TEXT dst
        TEXT relation
        TEXT confidence
    }
    files ||--o{ symbols : "contains"
    symbols ||--o{ bands : "has"
    symbols ||--o| embeddings : "has"
    graph_nodes ||--o{ graph_edges : "src, dst"
```
