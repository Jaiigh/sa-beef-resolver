PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS beefs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  person_a TEXT NOT NULL,
  person_b TEXT NOT NULL,
  reason TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'resolved')),
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CHECK (person_a <> person_b),
  UNIQUE (person_a, person_b)
);

INSERT INTO beefs (person_a, person_b, reason, status) VALUES
  ('A', 'B', 'They disagree about who borrowed the headphones.', 'active'),
  ('A', 'C', 'They argued over a group-project contribution.', 'active'),
  ('A', 'D', 'They cannot agree on a football team.', 'active'),
  ('A', 'E', 'A missed birthday invitation caused an argument.', 'resolved'),
  ('B', 'A', 'They disagree about who borrowed the headphones.', 'active'),
  ('B', 'C', 'They argued about the queue for lunch.', 'active'),
  ('B', 'X', 'They have an unresolved gaming match dispute.', 'active');
