\set ON_ERROR_STOP on
\pset pager off
\pset footer off

BEGIN READ ONLY;

\echo === Журнал событий: первая страница ===
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)
WITH audit_entries AS (
    SELECT 'app_user'::text AS source_table, 0 AS source_order, id_audit, operation, changed_at,
           changed_by_user_id, NULL::text AS related_kind, NULL::text AS related_id
    FROM public.app_user_l
    UNION ALL
    SELECT 'organization', 1, id_audit, operation, changed_at, changed_by_user_id, NULL::text, NULL::text
    FROM public.organization_l
    UNION ALL
    SELECT 'survey', 2, id_audit, operation, changed_at, changed_by_user_id,
           'survey'::text, id_survey::text
    FROM public.survey_l
    UNION ALL
    SELECT 'survey_question', 3, id_audit, operation, changed_at, changed_by_user_id,
           'survey'::text, id_survey::text
    FROM public.survey_question_l
    UNION ALL
    SELECT 'organization_survey', 4, id_audit, operation, changed_at, changed_by_user_id,
           'survey'::text, id_survey::text
    FROM public.organization_survey_l
    UNION ALL
    SELECT 'answer', 5, id_audit, operation, changed_at, changed_by_user_id,
           'answer'::text, id_answer::text
    FROM public.answer_l
    UNION ALL
    SELECT 'answer_item', 6, id_audit, operation, changed_at, changed_by_user_id,
           'answer'::text, id_answer::text
    FROM public.answer_item_l
    UNION ALL
    SELECT 'auto_creation_config', 7, id_audit, operation, changed_at, changed_by_user_id,
           'auto_creation_config'::text, id_config::text
    FROM public.auto_creation_config_l
    UNION ALL
    SELECT 'survey_auto_creation_config', 8, id_audit, operation, changed_at, changed_by_user_id,
           'auto_creation_config'::text, id_config::text
    FROM public.survey_auto_creation_config_l
    UNION ALL
    SELECT 'email_config', 9, id_audit, operation, changed_at, changed_by_user_id,
           'email_config'::text, id_config::text
    FROM public.email_config_l
    UNION ALL
    SELECT 'theme_config', 10, id_audit, operation, changed_at, changed_by_user_id,
           'theme_config'::text, id_config::text
    FROM public.theme_config_l
),
event_entries AS (
    SELECT *,
        CASE
            WHEN related_kind IS NOT NULL AND related_id IS NOT NULL THEN concat(
                'related|', related_kind, '|', related_id, '|',
                coalesce(changed_by_user_id::text, ''), '|', changed_at::text)
            ELSE concat('audit|', source_table, '|', id_audit::text)
        END AS event_key
    FROM audit_entries
),
event_groups AS (
    SELECT event_key,
           max(changed_at) AS changed_at,
           max(source_order) AS source_order,
           max(id_audit) AS id_audit
    FROM event_entries
    GROUP BY event_key
)
SELECT event_key
FROM event_groups
ORDER BY changed_at DESC, source_order DESC, id_audit DESC
OFFSET 0
LIMIT :plan_limit;

\echo === Архив анкет администратора: первая страница ===
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)
WITH survey_rows AS (
    SELECT
        survey.id_survey,
        survey.name_survey,
        schedule.date_begin,
        schedule.date_end,
        COALESCE(ARRAY(
            SELECT DISTINCT assignment.id_organization
            FROM public.organization_survey assignment
            WHERE assignment.id_survey = survey.id_survey
              AND assignment.id_organization IS NOT NULL
            ORDER BY assignment.id_organization
        ), ARRAY[]::integer[]) AS organization_ids
    FROM public.survey survey
    LEFT JOIN public.survey_schedule schedule ON schedule.id_survey = survey.id_survey
    WHERE EXISTS (
        SELECT 1
        FROM public.organization_survey existing_assignment
        WHERE existing_assignment.id_survey = survey.id_survey
    )
      AND EXISTS (
        SELECT 1
        FROM public.answer answer
        INNER JOIN public.organization_survey answered_assignment
            ON answered_assignment.id_organization_survey = answer.id_organization_survey
        WHERE answered_assignment.id_survey = survey.id_survey
    )
      AND NOT EXISTS (
        SELECT 1
        FROM public.organization_survey active_assignment
        WHERE active_assignment.id_survey = survey.id_survey
          AND (active_assignment.date_end IS NULL OR active_assignment.date_end >= CURRENT_DATE)
    )
)
SELECT id_survey, name_survey, date_begin, date_end, organization_ids
FROM survey_rows
ORDER BY id_survey DESC
OFFSET 0
LIMIT :plan_limit;

\echo === Архив анкет клиента: первая страница ===
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)
WITH sample_organization AS (
    SELECT id_organization
    FROM public.organization_survey
    ORDER BY id_organization
    LIMIT 1
)
SELECT
    survey.id_survey,
    survey.name_survey,
    answer.completion_date,
    answer.csp
FROM public.survey survey
INNER JOIN public.organization_survey assignment
    ON assignment.id_survey = survey.id_survey
INNER JOIN public.answer answer
    ON answer.id_organization_survey = assignment.id_organization_survey
WHERE assignment.id_organization = (SELECT id_organization FROM sample_organization)
ORDER BY answer.completion_date DESC
OFFSET 0
LIMIT :plan_limit;

\echo === Отчеты: ответы анкеты ===
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)
WITH sample_survey AS (
    SELECT assignment.id_survey
    FROM public.answer answer
    INNER JOIN public.organization_survey assignment
        ON assignment.id_organization_survey = answer.id_organization_survey
    ORDER BY answer.completion_date DESC NULLS LAST
    LIMIT 1
)
SELECT
    answer.id_answer,
    answer.id_organization_survey,
    assignment.id_organization,
    answer.completion_date,
    organization.organization_name
FROM public.answer answer
INNER JOIN public.organization_survey assignment
    ON assignment.id_organization_survey = answer.id_organization_survey
LEFT JOIN public.organization organization
    ON organization.id_organization = assignment.id_organization
WHERE assignment.id_survey = (SELECT id_survey FROM sample_survey)
  AND EXISTS (
      SELECT 1
      FROM public.answer_item item
      WHERE item.id_answer = answer.id_answer
  )
ORDER BY answer.completion_date DESC;

\echo === Отчеты: строки ответов выбранной анкеты ===
EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)
WITH sample_answers AS (
    SELECT answer.id_answer
    FROM public.answer answer
    INNER JOIN public.organization_survey assignment
        ON assignment.id_organization_survey = answer.id_organization_survey
    WHERE assignment.id_survey = (
        SELECT id_survey
        FROM public.organization_survey
        ORDER BY id_survey DESC
        LIMIT 1
    )
    ORDER BY answer.completion_date DESC NULLS LAST
    LIMIT :plan_limit
)
SELECT item.id_answer, item.question_order, item.question_text, item.rating, item.comment
FROM public.answer_item item
WHERE item.id_answer IN (SELECT id_answer FROM sample_answers)
ORDER BY item.id_answer, item.question_order;

ROLLBACK;
