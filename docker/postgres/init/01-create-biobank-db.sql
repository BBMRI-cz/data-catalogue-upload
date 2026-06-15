-- Runs once, on first initialization of an empty Postgres data directory.
-- The uploader's database is created from POSTGRES_DB; the biobank_api service
-- uses its own database, created here.
CREATE DATABASE biobank_api;
