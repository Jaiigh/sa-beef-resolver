CREATE TABLE IF NOT EXISTS beefs (
  id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  person_a TEXT NOT NULL,
  person_b TEXT NOT NULL,
  reason TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'resolved')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CHECK (person_a <> person_b),
  UNIQUE (person_a, person_b)
);
