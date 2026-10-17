# Lens: Time and storage

For every instant the design stores or compares: is its time zone stated, is the stored form the one the comparison
uses, and is the boundary (before, at, after) decided? Flag an instant stored as local time with no zone, a comparison
across two forms, and a column type that cannot be ordered. A design that stores a time zone-free value for something
compared against the server clock is HIGH.

Severities (design): CRITICAL = the design cannot be built or would break shipped behaviour; HIGH = a likely defect or an unmet acceptance item; MEDIUM = a gap a builder would have to guess at; LOW = wording or polish.
