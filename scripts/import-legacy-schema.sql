\set ON_ERROR_STOP on

\if :{?legacy_schema}
\else
    \echo 'Set legacy_schema, for example: --set=legacy_schema=legacy'
    DO $$ BEGIN RAISE EXCEPTION 'legacy_schema is required'; END $$;
\endif

\if :{?legacy_password_hash}
\else
    \echo 'Set legacy_password_hash. Use scripts/import-legacy-schema.ps1 to generate it safely.'
    DO $$ BEGIN RAISE EXCEPTION 'legacy_password_hash is required'; END $$;
\endif

\if :{?commit_import}
\else
    \set commit_import true
\endif

SELECT :'legacy_schema' <> 'public' AS source_schema_is_safe \gset
\if :source_schema_is_safe
\else
    \echo 'The legacy schema must not be public.'
    DO $$ BEGIN RAISE EXCEPTION 'public cannot be used as the legacy schema'; END $$;
\endif

SELECT COUNT(*) = 11 AS source_tables_are_present
FROM information_schema.tables
WHERE table_schema = :'legacy_schema'
  AND table_name IN (
      'organisation',
      'users',
      'roles',
      'io',
      'surveys',
      'questions',
      'surveys_questions',
      'access_to_survey',
      'survey_response',
      'user_answers',
      'csp'
  ) \gset

\if :source_tables_are_present
\else
    \echo 'The legacy schema does not contain all expected tables.'
    DO $$ BEGIN RAISE EXCEPTION 'legacy tables are missing'; END $$;
\endif

WITH required_columns(table_name, column_name) AS (
    VALUES
        ('organisation', 'id'),
        ('organisation', 'name'),
        ('organisation', 'street'),
        ('organisation', 'house'),
        ('users', 'id'),
        ('users', 'login'),
        ('users', 'password'),
        ('users', 'role_id'),
        ('users', 'surname'),
        ('users', 'name'),
        ('users', 'patronymic'),
        ('users', 'email'),
        ('users', 'date_begin'),
        ('users', 'date_end'),
        ('roles', 'id'),
        ('roles', 'role'),
        ('io', 'id'),
        ('io', 'user_id'),
        ('io', 'organisation_id'),
        ('io', 'date_begin'),
        ('io', 'date_end'),
        ('surveys', 'id'),
        ('surveys', 'name'),
        ('surveys', 'start_date'),
        ('surveys', 'end_date'),
        ('surveys', 'last_month'),
        ('surveys', 'blocked'),
        ('surveys', 'description'),
        ('questions', 'id'),
        ('questions', 'name'),
        ('surveys_questions', 'id'),
        ('surveys_questions', 'question_id'),
        ('surveys_questions', 'survey_id'),
        ('access_to_survey', 'id'),
        ('access_to_survey', 'survey_id'),
        ('access_to_survey', 'io_id'),
        ('access_to_survey', 'begin_date'),
        ('access_to_survey', 'end_date'),
        ('survey_response', 'id'),
        ('survey_response', 'io_id'),
        ('survey_response', 'survey_id'),
        ('survey_response', 'date'),
        ('survey_response', 'id_csp'),
        ('user_answers', 'id'),
        ('user_answers', 'question_id'),
        ('user_answers', 'survey_response_id'),
        ('user_answers', 'answer'),
        ('user_answers', 'comment'),
        ('csp', 'id'),
        ('csp', 'file_name_survey'),
        ('csp', 'survey'),
        ('csp', 'file_name_csp'),
        ('csp', 'csp')
)
SELECT NOT EXISTS (
    SELECT 1
    FROM required_columns required
    LEFT JOIN information_schema.columns actual
      ON actual.table_schema = :'legacy_schema'
     AND actual.table_name = required.table_name
     AND actual.column_name = required.column_name
    WHERE actual.column_name IS NULL
) AS source_columns_are_present \gset

\if :source_columns_are_present
\else
    \echo 'The legacy schema does not have the expected column set.'
    DO $$ BEGIN RAISE EXCEPTION 'legacy columns are missing'; END $$;
\endif

CREATE TEMP TABLE _required_target_schema (
    object_name text PRIMARY KEY,
    is_present boolean NOT NULL
);

INSERT INTO _required_target_schema (object_name, is_present)
VALUES
    ('table public.organization', to_regclass('public.organization') IS NOT NULL),
    ('table public.app_user', to_regclass('public.app_user') IS NOT NULL),
    ('table public.survey', to_regclass('public.survey') IS NOT NULL),
    ('table public.survey_question', to_regclass('public.survey_question') IS NOT NULL),
    ('table public.organization_survey', to_regclass('public.organization_survey') IS NOT NULL),
    ('table public.answer', to_regclass('public.answer') IS NOT NULL),
    ('table public.answer_item', to_regclass('public.answer_item') IS NOT NULL),
    ('table public.answer_participant', to_regclass('public.answer_participant') IS NOT NULL),
    (
        'column public.survey.date_begin',
        EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'survey'
              AND column_name = 'date_begin'
        )
    );

SELECT object_name AS missing_target_object
FROM _required_target_schema
WHERE NOT is_present
ORDER BY object_name;

SELECT COALESCE(BOOL_AND(is_present), false) AS target_schema_is_current
FROM _required_target_schema \gset

\if :target_schema_is_current
\else
    \echo 'Apply all current database migrations before importing legacy data.'
    DO $$ BEGIN RAISE EXCEPTION 'target schema is not current'; END $$;
\endif

BEGIN;
SELECT pg_advisory_xact_lock(hashtext('survey:legacy-schema-import'));

CREATE TEMP TABLE _legacy_organization ON COMMIT DROP AS
SELECT
    organization.id::bigint AS old_id,
    NULLIF(BTRIM(organization.name::text), '') AS organization_name,
    NULLIF(BTRIM(CONCAT_WS(' ', organization.street::text, organization.house::text)), '') AS legacy_address,
    COALESCE(MIN(link.date_begin::date), CURRENT_DATE) AS date_begin,
    CASE
        WHEN COUNT(link.id) = 0 OR BOOL_OR(link.date_end IS NULL) THEN NULL::date
        ELSE MAX(link.date_end::date)
    END AS date_end
FROM :"legacy_schema".organisation organization
LEFT JOIN :"legacy_schema".io link
  ON link.organisation_id = organization.id
GROUP BY organization.id, organization.name, organization.street, organization.house;

INSERT INTO _legacy_organization (
    old_id,
    organization_name,
    legacy_address,
    date_begin,
    date_end
)
SELECT
    -9223372036854775807::bigint,
    'Системные администраторы (импорт)',
    NULL::text,
    COALESCE(MIN(legacy_user.date_begin::date), CURRENT_DATE),
    CASE
        WHEN BOOL_OR(legacy_user.date_end IS NULL) THEN NULL::date
        ELSE MAX(legacy_user.date_end::date)
    END
FROM :"legacy_schema".users legacy_user
LEFT JOIN :"legacy_schema".roles role
  ON role.id = legacy_user.role_id
LEFT JOIN :"legacy_schema".io link
  ON link.user_id = legacy_user.id
WHERE link.id IS NULL
  AND LOWER(BTRIM(role.role::text)) IN ('admin', 'administrator', 'администратор')
HAVING COUNT(*) > 0;

CREATE TEMP TABLE _legacy_user ON COMMIT DROP AS
WITH user_with_latest_link AS (
    SELECT
        legacy_user.*,
        role.role::text AS role_name,
        link.id::bigint AS link_id,
        link.organisation_id::bigint AS link_organization_id,
        link.date_begin::date AS link_date_begin,
        link.date_end::date AS link_date_end
    FROM :"legacy_schema".users legacy_user
    LEFT JOIN :"legacy_schema".roles role
      ON role.id = legacy_user.role_id
    LEFT JOIN LATERAL (
        SELECT candidate.*
        FROM :"legacy_schema".io candidate
        WHERE candidate.user_id = legacy_user.id
        ORDER BY candidate.date_begin DESC NULLS LAST, candidate.id DESC
        LIMIT 1
    ) link ON true
), normalized_user AS (
    SELECT
        source.*,
        CASE LOWER(BTRIM(source.role_name))
            WHEN 'admin' THEN 'admin'
            WHEN 'administrator' THEN 'admin'
            WHEN 'администратор' THEN 'admin'
            WHEN 'user' THEN 'user'
            WHEN 'клиент' THEN 'user'
            WHEN 'пользователь' THEN 'user'
            ELSE NULL
        END AS normalized_role
    FROM user_with_latest_link source
)
SELECT
    legacy_user.id::bigint AS old_id,
    legacy_user.link_id AS old_io_id,
    COALESCE(
        legacy_user.link_organization_id,
        CASE
            WHEN legacy_user.normalized_role = 'admin' THEN -9223372036854775807::bigint
            ELSE NULL::bigint
        END
    ) AS old_organization_id,
    NULLIF(BTRIM(legacy_user.login::text), '') AS login,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(
            ' ',
            legacy_user.surname::text,
            legacy_user.name::text,
            legacy_user.patronymic::text
        )), ''),
        NULLIF(BTRIM(legacy_user.login::text), '')
    ) AS full_name,
    legacy_user.normalized_role,
    CASE
        WHEN legacy_user.password::text LIKE 'AQAAAA%' THEN legacy_user.password::text
        ELSE :'legacy_password_hash'
    END AS password_hash,
    legacy_user.password::text LIKE 'AQAAAA%' AS preserved_password,
    NULLIF(BTRIM(legacy_user.email::text), '') AS email,
    GREATEST(
        COALESCE(legacy_user.date_begin::date, legacy_user.link_date_begin, CURRENT_DATE),
        COALESCE(legacy_user.link_date_begin, legacy_user.date_begin::date, CURRENT_DATE)
    ) AS date_begin,
    CASE
        WHEN legacy_user.date_end IS NULL THEN legacy_user.link_date_end
        WHEN legacy_user.link_date_end IS NULL THEN legacy_user.date_end::date
        ELSE LEAST(legacy_user.date_end::date, legacy_user.link_date_end)
    END AS date_end
FROM normalized_user legacy_user;

CREATE TEMP TABLE _legacy_access_assignment ON COMMIT DROP AS
SELECT
    access.survey_id::bigint AS old_survey_id,
    link.organisation_id::bigint AS old_organization_id,
    MIN(access.begin_date::date) AS date_begin,
    CASE
        WHEN BOOL_OR(access.end_date IS NULL) THEN NULL::date
        ELSE MAX(access.end_date::date)
    END AS date_end
FROM :"legacy_schema".access_to_survey access
LEFT JOIN :"legacy_schema".io link
  ON link.id = access.io_id
GROUP BY access.survey_id, link.organisation_id;

CREATE TEMP TABLE _legacy_response ON COMMIT DROP AS
SELECT
    response.id::bigint AS old_id,
    response.survey_id::bigint AS old_survey_id,
    link.organisation_id::bigint AS old_organization_id,
    link.user_id::bigint AS old_user_id,
    response.date::timestamp without time zone AS completion_date,
    response.id_csp::bigint AS old_csp_id
FROM :"legacy_schema".survey_response response
LEFT JOIN :"legacy_schema".io link
  ON link.id = response.io_id;

CREATE TEMP TABLE _legacy_survey ON COMMIT DROP AS
WITH access_period AS (
    SELECT
        assignment.old_survey_id,
        MIN(assignment.date_begin) AS date_begin,
        CASE
            WHEN BOOL_OR(assignment.date_end IS NULL) THEN NULL::date
            ELSE MIN(assignment.date_end)
        END AS date_end
    FROM _legacy_access_assignment assignment
    GROUP BY assignment.old_survey_id
), response_period AS (
    SELECT
        response.old_survey_id,
        MIN(response.completion_date::date) AS date_begin,
        MAX(response.completion_date::date) AS date_end
    FROM _legacy_response response
    GROUP BY response.old_survey_id
)
SELECT
    survey.id::bigint AS old_id,
    NULLIF(BTRIM(survey.name::text), '') AS survey_name,
    NULLIF(BTRIM(survey.description::text), '') AS description,
    COALESCE(
        access_period.date_begin,
        survey.start_date::date,
        response_period.date_begin
    ) AS date_begin,
    CASE
        WHEN access_period.old_survey_id IS NOT NULL THEN access_period.date_end
        ELSE COALESCE(survey.end_date::date, response_period.date_end)
    END AS date_end,
    survey.start_date::date AS legacy_date_begin,
    survey.end_date::date AS legacy_date_end,
    COALESCE(
        LOWER(BTRIM(survey.last_month::text)) IN ('1', 'true', 't', 'yes', 'y'),
        false
    ) AS was_last_month,
    COALESCE(
        LOWER(BTRIM(survey.blocked::text)) IN ('1', 'true', 't', 'yes', 'y'),
        false
    ) AS was_blocked
FROM :"legacy_schema".surveys survey
LEFT JOIN access_period
  ON access_period.old_survey_id = survey.id
LEFT JOIN response_period
  ON response_period.old_survey_id = survey.id;

CREATE TEMP TABLE _legacy_assignment ON COMMIT DROP AS
SELECT
    assignment.old_survey_id,
    assignment.old_organization_id,
    assignment.date_begin,
    assignment.date_end,
    false AS was_reconstructed
FROM _legacy_access_assignment assignment
UNION ALL
SELECT DISTINCT
    response.old_survey_id,
    response.old_organization_id,
    COALESCE(survey.legacy_date_begin, response.completion_date::date) AS date_begin,
    COALESCE(survey.legacy_date_end, response.completion_date::date) AS date_end,
    true AS was_reconstructed
FROM _legacy_response response
INNER JOIN _legacy_survey survey
  ON survey.old_id = response.old_survey_id
WHERE NOT EXISTS (
    SELECT 1
    FROM _legacy_access_assignment assignment
    WHERE assignment.old_survey_id = response.old_survey_id
      AND assignment.old_organization_id = response.old_organization_id
);

CREATE TEMP TABLE _legacy_question ON COMMIT DROP AS
WITH deduplicated_relation AS (
    SELECT DISTINCT ON (relation.survey_id, relation.question_id)
        relation.id,
        relation.survey_id,
        relation.question_id
    FROM :"legacy_schema".surveys_questions relation
    ORDER BY relation.survey_id, relation.question_id, relation.id
)
SELECT
    relation.id::bigint AS old_relation_id,
    relation.survey_id::bigint AS old_survey_id,
    relation.question_id::bigint AS old_question_id,
    ROW_NUMBER() OVER (
        PARTITION BY relation.survey_id
        ORDER BY relation.id
    )::integer AS question_order,
    NULLIF(BTRIM(question.name::text), '') AS question_text
FROM deduplicated_relation relation
LEFT JOIN :"legacy_schema".questions question
  ON question.id = relation.question_id;

CREATE TEMP TABLE _legacy_existing_survey ON COMMIT DROP AS
SELECT
    legacy_survey.old_id AS old_survey_id,
    target_survey.id_survey AS target_survey_id
FROM _legacy_survey legacy_survey
INNER JOIN public.survey target_survey
  ON LOWER(BTRIM(target_survey.name_survey)) = LOWER(legacy_survey.survey_name)
 AND target_survey.date_begin IS NOT DISTINCT FROM legacy_survey.date_begin
 AND target_survey.date_end IS NOT DISTINCT FROM legacy_survey.date_end;

CREATE TEMP TABLE _legacy_answer_item_raw ON COMMIT DROP AS
SELECT
    item.id::bigint AS old_id,
    item.survey_response_id::bigint AS old_response_id,
    response.survey_id::bigint AS old_survey_id,
    item.question_id::bigint AS old_question_id,
    NULLIF(BTRIM(item.answer::text), '') AS rating_text,
    NULLIF(BTRIM(item.comment::text), '') AS comment
FROM :"legacy_schema".user_answers item
LEFT JOIN :"legacy_schema".survey_response response
  ON response.id = item.survey_response_id;

CREATE TEMP TABLE _legacy_answer_item ON COMMIT DROP AS
SELECT DISTINCT ON (old_response_id, old_question_id)
    old_id,
    old_response_id,
    old_survey_id,
    old_question_id,
    rating_text,
    comment
FROM _legacy_answer_item_raw
ORDER BY old_response_id, old_question_id, old_id;

SELECT CASE udt_name
    WHEN 'bytea' THEN 'NULLIF(encode(csp, ''base64''), '''')'
    WHEN 'oid' THEN 'NULLIF(encode(lo_get(csp), ''base64''), '''')'
    ELSE 'NULLIF(BTRIM(csp::text), '''')'
END AS legacy_signature_expression
FROM information_schema.columns
WHERE table_schema = :'legacy_schema'
  AND table_name = 'csp'
  AND column_name = 'csp' \gset

SELECT CASE udt_name
    WHEN 'bytea' THEN 'survey'
    WHEN 'oid' THEN 'lo_get(survey)'
    ELSE 'convert_to(COALESCE(survey::text, ''''), ''UTF8'')'
END AS legacy_signed_content_expression
FROM information_schema.columns
WHERE table_schema = :'legacy_schema'
  AND table_name = 'csp'
  AND column_name = 'survey' \gset

CREATE TEMP TABLE _legacy_csp (
    old_id bigint PRIMARY KEY,
    signature text,
    signed_content bytea
) ON COMMIT DROP;

INSERT INTO _legacy_csp (old_id, signature, signed_content)
SELECT
    id::bigint,
    :legacy_signature_expression,
    :legacy_signed_content_expression
FROM :"legacy_schema".csp;

CREATE TEMP TABLE _legacy_import_problem (
    problem text NOT NULL,
    record_key text
) ON COMMIT DROP;

INSERT INTO _legacy_import_problem
SELECT 'Organization has an empty name', old_id::text
FROM _legacy_organization
WHERE organization_name IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Organization period is invalid', old_id::text
FROM _legacy_organization
WHERE date_end IS NOT NULL
  AND date_end <= date_begin;

INSERT INTO _legacy_import_problem
SELECT 'Non-administrator user has no IO row', legacy_user.id::text
FROM :"legacy_schema".users legacy_user
LEFT JOIN :"legacy_schema".roles role
  ON role.id = legacy_user.role_id
LEFT JOIN :"legacy_schema".io link
  ON link.user_id = legacy_user.id
GROUP BY legacy_user.id, role.role
HAVING COUNT(link.id) = 0
   AND LOWER(BTRIM(role.role::text)) NOT IN ('admin', 'administrator', 'администратор');

INSERT INTO _legacy_import_problem
SELECT 'User has an empty login', old_id::text
FROM _legacy_user
WHERE login IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'User has an unsupported role', old_id::text
FROM _legacy_user
WHERE normalized_role IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'User period is invalid', old_id::text
FROM _legacy_user
WHERE date_end IS NOT NULL
  AND date_end <= date_begin;

INSERT INTO _legacy_import_problem
SELECT 'Duplicate login in legacy schema', login
FROM _legacy_user
GROUP BY login
HAVING COUNT(*) > 1;

INSERT INTO _legacy_import_problem
SELECT 'Login already exists in public.app_user', legacy_user.login
FROM _legacy_user legacy_user
INNER JOIN public.app_user target_user
  ON target_user.login = legacy_user.login;

INSERT INTO _legacy_import_problem
SELECT 'Organization already exists in public.organization', legacy_organization.organization_name
FROM _legacy_organization legacy_organization
INNER JOIN public.organization target_organization
  ON LOWER(BTRIM(target_organization.organization_name)) = LOWER(legacy_organization.organization_name);

INSERT INTO _legacy_import_problem
SELECT 'Survey has an empty name', old_id::text
FROM _legacy_survey
WHERE survey_name IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Survey period is invalid', old_id::text
FROM _legacy_survey
WHERE date_begin IS NOT NULL
  AND date_end IS NOT NULL
  AND date_end <= date_begin;

INSERT INTO _legacy_import_problem
SELECT 'Blocked survey has not expired and requires a manual decision', old_id::text
FROM _legacy_survey
WHERE was_blocked
  AND (date_end IS NULL OR date_end >= CURRENT_DATE);

INSERT INTO _legacy_import_problem
SELECT 'More than one target survey matches a legacy survey', old_survey_id::text
FROM _legacy_existing_survey
GROUP BY old_survey_id
HAVING COUNT(*) > 1;

INSERT INTO _legacy_import_problem
SELECT 'Target survey matches more than one legacy survey', target_survey_id::text
FROM _legacy_existing_survey
GROUP BY target_survey_id
HAVING COUNT(*) > 1;

INSERT INTO _legacy_import_problem
SELECT DISTINCT
    'Existing survey questions differ from legacy survey', existing.old_survey_id::text
FROM _legacy_existing_survey existing
WHERE EXISTS (
        SELECT 1
        FROM public.survey_question target_question
        WHERE target_question.id_survey = existing.target_survey_id
    )
  AND (
        EXISTS (
            SELECT 1
            FROM _legacy_question legacy_question
            LEFT JOIN public.survey_question target_question
              ON target_question.id_survey = existing.target_survey_id
             AND target_question.question_order = legacy_question.question_order
             AND BTRIM(target_question.question_text) = BTRIM(legacy_question.question_text)
            WHERE legacy_question.old_survey_id = existing.old_survey_id
              AND target_question.id_question IS NULL
        )
        OR EXISTS (
            SELECT 1
            FROM public.survey_question target_question
            LEFT JOIN _legacy_question legacy_question
              ON legacy_question.old_survey_id = existing.old_survey_id
             AND legacy_question.question_order = target_question.question_order
             AND BTRIM(legacy_question.question_text) = BTRIM(target_question.question_text)
            WHERE target_question.id_survey = existing.target_survey_id
              AND legacy_question.old_relation_id IS NULL
        )
    );

INSERT INTO _legacy_import_problem
SELECT 'Question relation points to a missing or empty question', old_relation_id::text
FROM _legacy_question
WHERE question_text IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Assignment points to a missing organization or survey', CONCAT(old_organization_id, '/', old_survey_id)
FROM _legacy_assignment assignment
LEFT JOIN _legacy_organization organization
  ON organization.old_id = assignment.old_organization_id
LEFT JOIN _legacy_survey survey
  ON survey.old_id = assignment.old_survey_id
WHERE organization.old_id IS NULL
   OR survey.old_id IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Assignment period is invalid', CONCAT(old_organization_id, '/', old_survey_id)
FROM _legacy_assignment
WHERE date_end IS NOT NULL
  AND date_end <= date_begin;

INSERT INTO _legacy_import_problem
SELECT 'More than one final response exists for an organization and survey', CONCAT(old_organization_id, '/', old_survey_id)
FROM _legacy_response
GROUP BY old_organization_id, old_survey_id
HAVING COUNT(*) > 1;

INSERT INTO _legacy_import_problem
SELECT 'Response points to a missing user, organization, or survey', response.old_id::text
FROM _legacy_response response
LEFT JOIN _legacy_user legacy_user
  ON legacy_user.old_id = response.old_user_id
LEFT JOIN _legacy_organization organization
  ON organization.old_id = response.old_organization_id
LEFT JOIN _legacy_survey survey
  ON survey.old_id = response.old_survey_id
WHERE legacy_user.old_id IS NULL
   OR organization.old_id IS NULL
   OR survey.old_id IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Answer item has a rating outside 1..5', old_id::text
FROM _legacy_answer_item
WHERE rating_text IS NULL
   OR rating_text !~ '^[1-5]$';

INSERT INTO _legacy_import_problem
SELECT 'Answer item cannot be matched to a survey question', item.old_id::text
FROM _legacy_answer_item item
LEFT JOIN _legacy_question question
  ON question.old_survey_id = item.old_survey_id
 AND question.old_question_id = item.old_question_id
WHERE question.old_relation_id IS NULL;

INSERT INTO _legacy_import_problem
SELECT 'Duplicated answer items contain different values', CONCAT(old_response_id, '/', old_question_id)
FROM _legacy_answer_item_raw
GROUP BY old_response_id, old_question_id
HAVING COUNT(*) > 1
   AND COUNT(DISTINCT (rating_text, comment)) > 1;

SELECT problem, record_key
FROM _legacy_import_problem
ORDER BY problem, record_key;

SELECT COUNT(*) = 0 AS preflight_passed
FROM _legacy_import_problem \gset

\if :preflight_passed
\else
    \echo 'Legacy import was not started because the preflight checks found problems.'
    ROLLBACK;
    DO $$ BEGIN RAISE EXCEPTION 'legacy import preflight failed'; END $$;
\endif

\if :commit_import
\else
    SELECT
        (SELECT COUNT(*) FROM _legacy_organization) AS organizations_to_import,
        (SELECT COUNT(*) FROM _legacy_user) AS users_to_import,
        (SELECT COUNT(*) FROM _legacy_user WHERE NOT preserved_password) AS users_to_reset_password,
        (SELECT COUNT(*) FROM _legacy_survey) - (SELECT COUNT(*) FROM _legacy_existing_survey) AS surveys_to_create,
        (SELECT COUNT(*) FROM _legacy_existing_survey) AS surveys_to_reuse,
        (SELECT COUNT(*) FROM _legacy_question) AS questions_to_import,
        (SELECT COUNT(*) FROM _legacy_assignment) AS assignments_to_import,
        (SELECT COUNT(*) FROM _legacy_assignment WHERE was_reconstructed) AS historical_assignments_reconstructed,
        (SELECT COUNT(*) FROM _legacy_response) AS answers_to_import,
        (SELECT COUNT(*) FROM _legacy_answer_item) AS answer_items_to_import,
        (SELECT COUNT(*) FROM :"legacy_schema".surveys_questions) - (SELECT COUNT(*) FROM _legacy_question) AS question_relations_collapsed,
        (SELECT COUNT(*) FROM _legacy_answer_item_raw) - (SELECT COUNT(*) FROM _legacy_answer_item) AS answer_items_collapsed,
        (SELECT COUNT(*) FROM _legacy_organization WHERE legacy_address IS NOT NULL) AS addresses_not_imported,
        (SELECT COUNT(*) FROM _legacy_survey WHERE was_last_month) AS last_month_flags_not_imported;

    ROLLBACK;
    \echo 'Legacy import dry run completed. No target rows were changed.'
    \quit
\endif

DO $$
DECLARE
    sequence_row record;
    table_maximum bigint;
    sequence_value bigint;
    sequence_was_called boolean;
    synchronized_value bigint;
BEGIN
    FOR sequence_row IN
        SELECT
            table_namespace.nspname AS table_schema,
            table_relation.relname AS table_name,
            table_column.attname AS column_name,
            sequence_namespace.nspname AS sequence_schema,
            sequence_relation.relname AS sequence_name
        FROM pg_class sequence_relation
        INNER JOIN pg_namespace sequence_namespace
          ON sequence_namespace.oid = sequence_relation.relnamespace
        INNER JOIN pg_depend dependency
          ON dependency.objid = sequence_relation.oid
         AND dependency.deptype IN ('a', 'i')
        INNER JOIN pg_class table_relation
          ON table_relation.oid = dependency.refobjid
        INNER JOIN pg_namespace table_namespace
          ON table_namespace.oid = table_relation.relnamespace
        INNER JOIN pg_attribute table_column
          ON table_column.attrelid = table_relation.oid
         AND table_column.attnum = dependency.refobjsubid
        WHERE sequence_relation.relkind = 'S'
          AND table_namespace.nspname = 'public'
    LOOP
        EXECUTE format(
            'SELECT MAX(%I)::bigint FROM %I.%I',
            sequence_row.column_name,
            sequence_row.table_schema,
            sequence_row.table_name
        ) INTO table_maximum;

        EXECUTE format(
            'SELECT last_value::bigint, is_called FROM %I.%I',
            sequence_row.sequence_schema,
            sequence_row.sequence_name
        ) INTO sequence_value, sequence_was_called;

        synchronized_value := GREATEST(
            COALESCE(table_maximum, 1),
            COALESCE(sequence_value, 1)
        );

        PERFORM setval(
            format('%I.%I', sequence_row.sequence_schema, sequence_row.sequence_name)::regclass,
            synchronized_value,
            sequence_was_called OR table_maximum IS NOT NULL
        );
    END LOOP;
END
$$;

CREATE TEMP TABLE _legacy_organization_map (
    old_id bigint PRIMARY KEY,
    new_id integer NOT NULL UNIQUE
) ON COMMIT DROP;

CREATE TEMP TABLE _legacy_user_map (
    old_id bigint PRIMARY KEY,
    new_id integer NOT NULL UNIQUE
) ON COMMIT DROP;

CREATE TEMP TABLE _legacy_survey_map (
    old_id bigint PRIMARY KEY,
    new_id integer NOT NULL UNIQUE
) ON COMMIT DROP;

CREATE TEMP TABLE _legacy_question_map (
    old_relation_id bigint PRIMARY KEY,
    new_id integer NOT NULL UNIQUE
) ON COMMIT DROP;

CREATE TEMP TABLE _legacy_assignment_map (
    old_survey_id bigint NOT NULL,
    old_organization_id bigint NOT NULL,
    new_id integer NOT NULL UNIQUE,
    PRIMARY KEY (old_survey_id, old_organization_id)
) ON COMMIT DROP;

CREATE TEMP TABLE _legacy_response_map (
    old_id bigint PRIMARY KEY,
    new_id integer NOT NULL UNIQUE
) ON COMMIT DROP;

INSERT INTO _legacy_survey_map (old_id, new_id)
SELECT old_survey_id, target_survey_id
FROM _legacy_existing_survey;

DO $$
DECLARE
    source_row record;
    inserted_id integer;
BEGIN
    FOR source_row IN SELECT * FROM _legacy_organization ORDER BY old_id LOOP
        INSERT INTO public.organization (
            organization_name,
            organization_short_name,
            date_begin,
            date_end
        )
        VALUES (
            source_row.organization_name,
            source_row.organization_name,
            source_row.date_begin,
            source_row.date_end
        )
        RETURNING id_organization INTO inserted_id;

        INSERT INTO _legacy_organization_map VALUES (source_row.old_id, inserted_id);
    END LOOP;

    FOR source_row IN SELECT * FROM _legacy_user ORDER BY old_id LOOP
        INSERT INTO public.app_user (
            id_organization,
            login,
            full_name,
            role,
            password,
            email,
            date_begin,
            date_end
        )
        SELECT
            organization_map.new_id,
            source_row.login,
            source_row.full_name,
            source_row.normalized_role,
            source_row.password_hash,
            source_row.email,
            source_row.date_begin,
            source_row.date_end
        FROM _legacy_organization_map organization_map
        WHERE organization_map.old_id = source_row.old_organization_id
        RETURNING id_user INTO inserted_id;

        INSERT INTO _legacy_user_map VALUES (source_row.old_id, inserted_id);
    END LOOP;

    FOR source_row IN
        SELECT legacy_survey.*
        FROM _legacy_survey legacy_survey
        LEFT JOIN _legacy_survey_map survey_map
          ON survey_map.old_id = legacy_survey.old_id
        WHERE survey_map.old_id IS NULL
        ORDER BY legacy_survey.old_id
    LOOP
        INSERT INTO public.survey (
            name_survey,
            description,
            date_begin,
            date_end
        )
        VALUES (
            source_row.survey_name,
            source_row.description,
            source_row.date_begin,
            source_row.date_end
        )
        RETURNING id_survey INTO inserted_id;

        INSERT INTO _legacy_survey_map VALUES (source_row.old_id, inserted_id);
    END LOOP;

    FOR source_row IN SELECT * FROM _legacy_question ORDER BY old_survey_id, question_order LOOP
        inserted_id := NULL;

        SELECT target_question.id_question
        INTO inserted_id
        FROM _legacy_survey_map survey_map
        INNER JOIN public.survey_question target_question
          ON target_question.id_survey = survey_map.new_id
         AND target_question.question_order = source_row.question_order
         AND BTRIM(target_question.question_text) = BTRIM(source_row.question_text)
        WHERE survey_map.old_id = source_row.old_survey_id;

        IF inserted_id IS NULL THEN
            INSERT INTO public.survey_question (
                id_survey,
                question_order,
                question_text
            )
            SELECT
                survey_map.new_id,
                source_row.question_order,
                source_row.question_text
            FROM _legacy_survey_map survey_map
            WHERE survey_map.old_id = source_row.old_survey_id
            RETURNING id_question INTO inserted_id;
        END IF;

        INSERT INTO _legacy_question_map VALUES (source_row.old_relation_id, inserted_id);
    END LOOP;

    FOR source_row IN SELECT * FROM _legacy_assignment ORDER BY old_survey_id, old_organization_id LOOP
        INSERT INTO public.organization_survey (
            id_organization,
            id_survey,
            date_begin,
            date_end
        )
        SELECT
            organization_map.new_id,
            survey_map.new_id,
            source_row.date_begin,
            source_row.date_end
        FROM _legacy_organization_map organization_map
        CROSS JOIN _legacy_survey_map survey_map
        WHERE organization_map.old_id = source_row.old_organization_id
          AND survey_map.old_id = source_row.old_survey_id
        RETURNING id_organization_survey INTO inserted_id;

        INSERT INTO _legacy_assignment_map VALUES (
            source_row.old_survey_id,
            source_row.old_organization_id,
            inserted_id
        );
    END LOOP;

    FOR source_row IN SELECT * FROM _legacy_response ORDER BY old_id LOOP
        INSERT INTO public.answer (
            id_organization_survey,
            completion_date,
            csp,
            signed_content
        )
        SELECT
            assignment_map.new_id,
            source_row.completion_date,
            signature.signature,
            NULLIF(signature.signed_content, ''::bytea)
        FROM _legacy_assignment_map assignment_map
        LEFT JOIN _legacy_csp signature
          ON signature.old_id = source_row.old_csp_id
        WHERE assignment_map.old_survey_id = source_row.old_survey_id
          AND assignment_map.old_organization_id = source_row.old_organization_id
        RETURNING id_answer INTO inserted_id;

        INSERT INTO _legacy_response_map VALUES (source_row.old_id, inserted_id);

        INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
        SELECT inserted_id, user_map.new_id, 'legacy'
        FROM _legacy_user_map user_map
        WHERE user_map.old_id = source_row.old_user_id;
    END LOOP;
END;
$$;

INSERT INTO public.answer_item (
    id_answer,
    question_order,
    question_text,
    rating,
    comment
)
SELECT
    response_map.new_id,
    question.question_order,
    question.question_text,
    item.rating_text::integer,
    CASE WHEN item.rating_text = '5' THEN NULL ELSE item.comment END
FROM _legacy_answer_item item
INNER JOIN _legacy_response_map response_map
  ON response_map.old_id = item.old_response_id
INNER JOIN _legacy_question question
  ON question.old_survey_id = item.old_survey_id
 AND question.old_question_id = item.old_question_id
ORDER BY response_map.new_id, question.question_order;

SELECT
    (SELECT COUNT(*) FROM _legacy_organization_map) AS organizations_imported,
    (SELECT COUNT(*) FROM _legacy_user_map) AS users_imported,
    (SELECT COUNT(*) FROM _legacy_user WHERE NOT preserved_password) AS users_with_temporary_password,
    (SELECT COUNT(*) FROM _legacy_survey_map) - (SELECT COUNT(*) FROM _legacy_existing_survey) AS surveys_created,
    (SELECT COUNT(*) FROM _legacy_existing_survey) AS surveys_reused,
    (SELECT COUNT(*) FROM _legacy_question_map) AS questions_imported,
    (SELECT COUNT(*) FROM _legacy_assignment_map) AS assignments_imported,
    (SELECT COUNT(*) FROM _legacy_assignment WHERE was_reconstructed) AS historical_assignments_reconstructed,
    (SELECT COUNT(*) FROM _legacy_response_map) AS answers_imported,
    (SELECT COUNT(*) FROM public.answer_item item INNER JOIN _legacy_response_map map ON map.new_id = item.id_answer) AS answer_items_imported,
    (SELECT COUNT(*) FROM :"legacy_schema".surveys_questions) - (SELECT COUNT(*) FROM _legacy_question) AS question_relations_collapsed,
    (SELECT COUNT(*) FROM _legacy_answer_item_raw) - (SELECT COUNT(*) FROM _legacy_answer_item) AS answer_items_collapsed,
    (SELECT COUNT(*) FROM _legacy_organization WHERE legacy_address IS NOT NULL) AS addresses_not_imported,
    (SELECT COUNT(*) FROM _legacy_survey WHERE was_last_month) AS last_month_flags_not_imported;

COMMIT;

\echo 'Legacy data import completed.'
