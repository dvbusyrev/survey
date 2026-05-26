\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '022') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 022_store_signed_answer_content

BEGIN;

ALTER TABLE public.answer
    ADD COLUMN IF NOT EXISTS signed_content bytea;

INSERT INTO public.schema_migrations (version, name)
VALUES ('022', 'store_signed_answer_content')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 022_store_signed_answer_content
\endif
