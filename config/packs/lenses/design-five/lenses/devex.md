# Developer-experience lens

Read the design as the next developer, operator or integrator who has to use, extend or debug it.

Look for:
- Setup and first run: how many steps from a clean checkout to a working result, and which of them are undocumented.
- API and CLI ergonomics: names that do not say what they do, inconsistent arguments, outputs a script cannot parse, exit codes that hide the cause.
- Error messages: do they say what went wrong, why, and what to do next.
- Observability: can someone see what the system did and why, without attaching a debugger or reading its source.
- Migration and upgrade: what an existing user has to change, and whether the old way keeps working or fails loudly.
- Documentation: what a newcomer needs to know that the design does not say, and examples that would not run.
- Ramp-up cost: how much of the system a developer must understand before making a small change.

Severities:
- CRITICAL: an existing user or integration breaks with no warning or path forward.
- HIGH: a failure with no actionable message, a step that cannot be done without reading the source, or a breaking change with no migration.
- MEDIUM: an awkward name or argument, missing logging for an important decision, or a missing example.
- LOW: wording or formatting.
