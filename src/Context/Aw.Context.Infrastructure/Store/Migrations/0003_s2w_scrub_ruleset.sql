-- Stage S-2W: every stored document records the scrub ruleset it was cleaned with (context spec §7, D14). A document
-- stored under another ruleset (the profile's extra patterns or the sanitizer changed; '' for rows S-1A wrote) is
-- skipped by search and cleaned again at the next refresh.
ALTER TABLE doc ADD COLUMN ruleset TEXT NOT NULL DEFAULT '';
