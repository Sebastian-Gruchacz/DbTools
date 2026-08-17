CREATE TABLE public.customer_data (
    id integer PRIMARY KEY,
    display_name varchar(64),
    notes text
);

CREATE TABLE public.__anonymyzer_detached_copy (
    marker_id uuid PRIMARY KEY,
    database_name text NOT NULL,
    created_utc timestamptz NOT NULL
);

INSERT INTO public.__anonymyzer_detached_copy (marker_id, database_name, created_utc)
VALUES ('11111111-2222-3333-4444-555555555555', current_database(), now());

CREATE TABLE public.labels (
    code varchar(12) PRIMARY KEY,
    value text NOT NULL
);

CREATE SCHEMA audit;

CREATE TABLE audit.customer_data (
    id integer PRIMARY KEY,
    payload text
);
