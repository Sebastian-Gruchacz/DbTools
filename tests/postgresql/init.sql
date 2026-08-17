CREATE TABLE public.customer_data (
    id integer PRIMARY KEY,
    display_name varchar(64),
    notes text
);

CREATE TABLE public.labels (
    code varchar(12) PRIMARY KEY,
    value text NOT NULL
);

CREATE SCHEMA audit;

CREATE TABLE audit.customer_data (
    id integer PRIMARY KEY,
    payload text
);
