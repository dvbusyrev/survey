BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_attribute a
            ON a.attrelid = c.conrelid
           AND a.attnum = ANY(c.conkey)
        WHERE c.contype = 'f'
          AND c.conrelid = 'public.organization_survey'::regclass
          AND c.confrelid = 'public.survey'::regclass
          AND a.attname = 'id_survey'
    ) THEN
        ALTER TABLE public.organization_survey
            ADD CONSTRAINT organization_survey_id_survey_fkey
            FOREIGN KEY (id_survey) REFERENCES public.survey (id_survey) ON DELETE CASCADE;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_attribute a
            ON a.attrelid = c.conrelid
           AND a.attnum = ANY(c.conkey)
        WHERE c.contype = 'f'
          AND c.conrelid = 'public.answer'::regclass
          AND c.confrelid = 'public.survey'::regclass
          AND a.attname = 'id_survey'
    ) THEN
        ALTER TABLE public.answer
            ADD CONSTRAINT answer_id_survey_fkey
            FOREIGN KEY (id_survey) REFERENCES public.survey (id_survey) ON DELETE CASCADE;
    END IF;
END $$;

COMMIT;
