\set ON_ERROR_STOP on

WITH role_usage AS (
    SELECT
        'role_usage'::text AS issue_type,
        role.id::text AS record_key,
        jsonb_build_object(
            'role', role.role,
            'user_count', COUNT(legacy_user.id)
        )::text AS details
    FROM testirovanie.roles role
    LEFT JOIN testirovanie.users legacy_user
      ON legacy_user.role_id = role.id
    GROUP BY role.id, role.role
),
unsupported_role_user AS (
    SELECT
        'unsupported_role_user'::text AS issue_type,
        legacy_user.id::text AS record_key,
        jsonb_build_object(
            'login', legacy_user.login,
            'role_id', legacy_user.role_id,
            'role', role.role
        )::text AS details
    FROM testirovanie.users legacy_user
    LEFT JOIN testirovanie.roles role
      ON role.id = legacy_user.role_id
    WHERE LOWER(BTRIM(role.role)) NOT IN (
        'admin',
        'administrator',
        'администратор',
        'user',
        'клиент'
    )
       OR role.role IS NULL
),
user_io_issue AS (
    SELECT
        'user_io_count'::text AS issue_type,
        legacy_user.id::text AS record_key,
        jsonb_build_object(
            'login', legacy_user.login,
            'io_count', COUNT(link.id),
            'io_rows', COALESCE(
                jsonb_agg(
                    jsonb_build_object(
                        'io_id', link.id,
                        'organization_id', link.organisation_id,
                        'date_begin', link.date_begin,
                        'date_end', link.date_end
                    )
                    ORDER BY link.id
                ) FILTER (WHERE link.id IS NOT NULL),
                '[]'::jsonb
            )
        )::text AS details
    FROM testirovanie.users legacy_user
    LEFT JOIN testirovanie.io link
      ON link.user_id = legacy_user.id
    GROUP BY legacy_user.id, legacy_user.login
    HAVING COUNT(link.id) <> 1
),
duplicate_login AS (
    SELECT
        'duplicate_login'::text AS issue_type,
        legacy_user.login::text AS record_key,
        jsonb_build_object(
            'users', jsonb_agg(
                jsonb_build_object(
                    'user_id', legacy_user.id,
                    'role_id', legacy_user.role_id,
                    'role', role.role
                )
                ORDER BY legacy_user.id
            )
        )::text AS details
    FROM testirovanie.users legacy_user
    LEFT JOIN testirovanie.roles role
      ON role.id = legacy_user.role_id
    GROUP BY legacy_user.login
    HAVING COUNT(*) > 1
),
duplicate_question AS (
    SELECT
        'duplicate_survey_question'::text AS issue_type,
        CONCAT(relation.survey_id, '/', relation.question_id) AS record_key,
        jsonb_build_object(
            'survey_name', survey.name,
            'question', question.name,
            'relations', jsonb_agg(
                jsonb_build_object(
                    'relation_id', relation.id,
                    'date_begin', relation.date_begin,
                    'date_end', relation.date_end
                )
                ORDER BY relation.id
            )
        )::text AS details
    FROM testirovanie.surveys_questions relation
    LEFT JOIN testirovanie.surveys survey
      ON survey.id = relation.survey_id
    LEFT JOIN testirovanie.questions question
      ON question.id = relation.question_id
    GROUP BY relation.survey_id, relation.question_id, survey.name, question.name
    HAVING COUNT(*) > 1
),
duplicate_answer_item AS (
    SELECT
        'duplicate_answer_item'::text AS issue_type,
        CONCAT(item.survey_response_id, '/', item.question_id) AS record_key,
        jsonb_build_object(
            'survey_id', response.survey_id,
            'io_id', response.io_id,
            'items', jsonb_agg(
                jsonb_build_object(
                    'item_id', item.id,
                    'answer', item.answer,
                    'comment', item.comment
                )
                ORDER BY item.id
            )
        )::text AS details
    FROM testirovanie.user_answers item
    LEFT JOIN testirovanie.survey_response response
      ON response.id = item.survey_response_id
    GROUP BY item.survey_response_id, item.question_id, response.survey_id, response.io_id
    HAVING COUNT(*) > 1
)
SELECT issue_type, record_key, details FROM role_usage
UNION ALL
SELECT issue_type, record_key, details FROM unsupported_role_user
UNION ALL
SELECT issue_type, record_key, details FROM user_io_issue
UNION ALL
SELECT issue_type, record_key, details FROM duplicate_login
UNION ALL
SELECT issue_type, record_key, details FROM duplicate_question
UNION ALL
SELECT issue_type, record_key, details FROM duplicate_answer_item
ORDER BY issue_type, record_key;
