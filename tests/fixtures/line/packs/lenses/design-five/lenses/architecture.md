# Lens: Architecture

Do the parts fit the code that exists? Look for two reads that can disagree, a second source of truth, a dependency pointing the wrong way, and a change that breaks a caller the design does not name.

Severities (design): CRITICAL = the design cannot be built or would break shipped behaviour; HIGH = a likely defect or an unmet acceptance item; MEDIUM = a gap a builder would have to guess at; LOW = wording or polish.
