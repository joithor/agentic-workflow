-- The tree a gate tested (git rev-parse HEAD^{tree} of the gate tree after the head was merged onto its base) and the pull requests
-- merged under it (one "<pr>:<head>" per line, empty for a normal gate). merge-check compares the tree with what merging the gated
-- head onto main gives today. Gates recorded before this keep a NULL tree and the old same-main rule.
ALTER TABLE gates ADD COLUMN tree TEXT;
ALTER TABLE gates ADD COLUMN stack TEXT NOT NULL DEFAULT '';
