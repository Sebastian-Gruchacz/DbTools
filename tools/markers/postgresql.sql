\if :{?marker_id}
\else
\error 'Pass marker_id with: psql -v marker_id=<guid> -f postgresql.sql'
\endif

CREATE TABLE public.__anonymyzer_detached_copy (
    marker_id uuid PRIMARY KEY,
    database_name text NOT NULL,
    created_utc timestamptz NOT NULL
);

INSERT INTO public.__anonymyzer_detached_copy (marker_id, database_name, created_utc)
VALUES (:'marker_id'::uuid, current_database(), now());
