-- The archive.
--
-- Two things in here are load-bearing and neither is a constraint.
--
-- `belongs` is stored, never derived. A portal that works out whose a document
-- is by looking at who asked for it has answered the wrong question, and that
-- is the shape of every leak this project was rebuilt from.
--
-- And there is no index on `id` alone that would tempt anybody to query by it.
-- The primary key is the accession because the archive upstream says so, but
-- the index the portal reads through is the one on `belongs`, because the only
-- question the portal asks is "what may this patient see".

CREATE TABLE IF NOT EXISTS documents (
  id         TEXT PRIMARY KEY,
  belongs    TEXT NOT NULL,
  title      TEXT NOT NULL,

  -- Released by a clinician. Until then it is a draft, and a draft handed to a
  -- patient is a diagnosis nobody has checked.
  released   INTEGER NOT NULL DEFAULT 0 CHECK (released IN (0, 1)),

  -- Needs a code sent to the patient's phone before it opens.
  sensitive  INTEGER NOT NULL DEFAULT 0 CHECK (sensitive IN (0, 1)),

  content    BLOB NOT NULL
);

CREATE INDEX IF NOT EXISTS documents_by_patient ON documents (belongs);

-- The audit trail.
--
-- Separate from the application log on purpose. Application logs are sampled,
-- turned down and rotated away; this is the answer to "who was handed my
-- report", which a hospital has to be able to give two years later.
--
-- `existed` is the column the patient never sees. It is what makes a refusal
-- honest in the record and silent to the person asking.

CREATE TABLE IF NOT EXISTS trail (
  at         TEXT NOT NULL,
  patient    TEXT NOT NULL,
  document   TEXT NOT NULL,
  given      INTEGER NOT NULL CHECK (given IN (0, 1)),
  refusal    TEXT NOT NULL,
  existed    INTEGER NOT NULL CHECK (existed IN (0, 1))
);

CREATE INDEX IF NOT EXISTS trail_by_patient ON trail (patient, at);
