-- A red step's flake classification is per test: a step is accepted only when every failing test in it was classified.
CREATE TABLE gate_reruns (
  pr INTEGER NOT NULL,
  run INTEGER NOT NULL,
  step TEXT NOT NULL,
  test TEXT NOT NULL,
  verdict TEXT NOT NULL CHECK (verdict IN ('Flake', 'Real')),
  PRIMARY KEY (pr, run, step, test),
  FOREIGN KEY (pr, run) REFERENCES gates(pr, run)
);

ALTER TABLE gate_steps DROP COLUMN flake;
