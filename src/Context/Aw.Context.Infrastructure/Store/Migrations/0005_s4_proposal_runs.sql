-- Stage S-4: a scoping run's whole surface list (context spec §8.7). A 'propose' override remembers only the surfaces that became
-- proposals; the graph status of a run lists every surface it submitted, the mapped and unmapped ones too, so the list is kept per
-- run as JSON (a second submission of the run replaces it).
CREATE TABLE proposal_run (
    run      TEXT PRIMARY KEY,
    repo     TEXT NOT NULL,
    surfaces TEXT NOT NULL
) STRICT;
