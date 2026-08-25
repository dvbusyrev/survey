\set ON_ERROR_STOP on

DO $$
DECLARE
    imported_organization_id integer;
    imported_user_id integer;
    imported_survey_id integer;
    imported_assignment_id integer;
    imported_answer_id integer;
    historical_organization_id integer;
    historical_survey_id integer;
    historical_assignment_id integer;
    historical_answer_id integer;
BEGIN
    SELECT id_organization
    INTO STRICT imported_organization_id
    FROM public.organization
    WHERE organization_name = 'Legacy import test organization';

    SELECT id_user
    INTO STRICT imported_user_id
    FROM public.app_user
    WHERE login = 'legacy-import-test-user'
      AND id_organization = imported_organization_id
      AND full_name = 'Тестов Легаси Импортович'
      AND role = 'user'
      AND password LIKE 'AQAAAA%';

    IF NOT EXISTS (
        SELECT 1
        FROM public.app_user imported_admin
        INNER JOIN public.organization imported_admin_organization
          ON imported_admin_organization.id_organization = imported_admin.id_organization
        WHERE imported_admin.login = 'legacy-import-admin'
          AND imported_admin.role = 'admin'
          AND imported_admin_organization.organization_name = 'Системные администраторы (импорт)'
    ) THEN
        RAISE EXCEPTION 'The administrator without a legacy IO row was not imported correctly.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.organization
        WHERE organization_name = 'Legacy historical organization'
    ) THEN
        RAISE EXCEPTION 'The historical user organization was not imported.';
    END IF;

    SELECT id_survey
    INTO STRICT imported_survey_id
    FROM public.survey
    WHERE name_survey = 'Legacy import test survey'
      AND date_begin = DATE '2026-08-01'
      AND date_end = DATE '2026-08-20';

    IF (
        SELECT description
        FROM public.survey
        WHERE id_survey = imported_survey_id
    ) <> 'Current survey description must be preserved' THEN
        RAISE EXCEPTION 'The existing target survey was overwritten instead of reused.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.survey
        WHERE name_survey = 'Legacy new survey'
          AND date_begin = DATE '2026-09-01'
          AND date_end = DATE '2026-09-20'
    ) THEN
        RAISE EXCEPTION 'The unmatched legacy survey was not created.';
    END IF;

    IF (
        SELECT COUNT(*)
        FROM public.survey_question
        WHERE id_survey = imported_survey_id
    ) <> 2 THEN
        RAISE EXCEPTION 'The reused target survey questions were duplicated or changed.';
    END IF;

    SELECT id_organization_survey
    INTO STRICT imported_assignment_id
    FROM public.organization_survey
    WHERE id_organization = imported_organization_id
      AND id_survey = imported_survey_id
      AND date_begin = DATE '2026-08-01'
      AND date_end = DATE '2026-08-20';

    SELECT id_answer
    INTO STRICT imported_answer_id
    FROM public.answer
    WHERE id_organization_survey = imported_assignment_id
      AND completion_date = TIMESTAMP '2026-08-10 00:00:00'
      AND convert_from(signed_content, 'UTF8') = 'legacy signed document'
      AND convert_from(decode(csp, 'base64'), 'UTF8') = 'legacy signature';

    IF NOT EXISTS (
        SELECT 1
        FROM public.answer_item
        WHERE id_answer = imported_answer_id
          AND question_order = 1
          AND question_text = 'Legacy server question one'
          AND rating = 5
          AND comment IS NULL
    ) THEN
        RAISE EXCEPTION 'The top-rating answer item was not imported correctly.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.answer_item
        WHERE id_answer = imported_answer_id
          AND question_order = 2
          AND question_text = 'Legacy server question two'
          AND rating = 4
          AND comment = 'Legacy comment'
    ) THEN
        RAISE EXCEPTION 'The commented answer item was not imported correctly.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.answer
        WHERE id_answer = imported_answer_id
          AND id_user = imported_user_id
    ) THEN
        RAISE EXCEPTION 'The legacy answer submitter was not imported correctly.';
    END IF;

    SELECT id_organization
    INTO STRICT historical_organization_id
    FROM public.organization
    WHERE organization_name = 'Legacy historical organization';

    SELECT id_survey
    INTO STRICT historical_survey_id
    FROM public.survey
    WHERE name_survey = 'Legacy new survey'
      AND date_begin = DATE '2026-09-01'
      AND date_end = DATE '2026-09-20';

    SELECT id_organization_survey
    INTO STRICT historical_assignment_id
    FROM public.organization_survey
    WHERE id_organization = historical_organization_id
      AND id_survey = historical_survey_id
      AND date_begin = DATE '2002-02-01'
      AND date_end = DATE '2002-02-28';

    SELECT id_answer
    INTO STRICT historical_answer_id
    FROM public.answer
    WHERE id_organization_survey = historical_assignment_id
      AND completion_date = TIMESTAMP '2026-09-10 00:00:00';

    IF NOT EXISTS (
        SELECT 1
        FROM public.answer_item
        WHERE id_answer = historical_answer_id
          AND question_order = 1
          AND question_text = 'Legacy new survey question'
          AND rating = 4
          AND comment = 'Historical response without retained access'
    ) THEN
        RAISE EXCEPTION 'The historical response without retained access was not imported correctly.';
    END IF;
END;
$$;
