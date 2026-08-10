\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS public.schema_migrations (
    version text PRIMARY KEY,
    name text NOT NULL,
    applied_at timestamp without time zone NOT NULL DEFAULT NOW(),
    date_update timestamp without time zone NOT NULL DEFAULT NOW(),
    user_update integer
);

SELECT CASE
    WHEN EXISTS (SELECT 1 FROM public.schema_migrations WHERE version = '035') THEN 'false'
    ELSE 'true'
END AS apply_migration \gset

\if :apply_migration
\echo Applying migration 035_disallow_comments_for_top_rating

BEGIN;

UPDATE public.answer_item
SET comment = NULL
WHERE rating = 5
  AND comment IS NOT NULL;

UPDATE public.answer_draft_item
SET comment = NULL
WHERE rating = 5
  AND comment IS NOT NULL;

ALTER TABLE public.answer_item
    DROP CONSTRAINT IF EXISTS answer_item_top_rating_comment_check;

ALTER TABLE public.answer_item
    ADD CONSTRAINT answer_item_top_rating_comment_check
    CHECK (rating IS DISTINCT FROM 5 OR comment IS NULL);

ALTER TABLE public.answer_draft_item
    DROP CONSTRAINT IF EXISTS answer_draft_item_top_rating_comment_check;

ALTER TABLE public.answer_draft_item
    ADD CONSTRAINT answer_draft_item_top_rating_comment_check
    CHECK (rating IS DISTINCT FROM 5 OR comment IS NULL);

INSERT INTO public.schema_migrations (version, name)
VALUES ('035', 'disallow_comments_for_top_rating')
ON CONFLICT (version) DO NOTHING;

COMMIT;

\else
\echo Skipping migration 035_disallow_comments_for_top_rating
\endif
