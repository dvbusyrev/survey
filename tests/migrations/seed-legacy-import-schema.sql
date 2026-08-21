\set ON_ERROR_STOP on

DROP SCHEMA IF EXISTS legacy_import_fixture CASCADE;
CREATE SCHEMA legacy_import_fixture;

CREATE TABLE legacy_import_fixture.organisation (
    id integer PRIMARY KEY,
    name text,
    street text,
    house text
);

CREATE TABLE legacy_import_fixture.roles (
    id smallint PRIMARY KEY,
    role text
);

CREATE TABLE legacy_import_fixture.users (
    id bigint PRIMARY KEY,
    login text,
    password text,
    role_id smallint,
    surname text,
    name text,
    patronymic text,
    email text,
    date_begin date,
    date_end date
);

CREATE TABLE legacy_import_fixture.io (
    id bigint PRIMARY KEY,
    user_id bigint,
    organisation_id smallint,
    date_begin date,
    date_end date
);

CREATE TABLE legacy_import_fixture.surveys (
    id bigint PRIMARY KEY,
    name text,
    start_date date,
    end_date date,
    last_month text,
    blocked bigint,
    description text
);

CREATE TABLE legacy_import_fixture.questions (
    id bigint PRIMARY KEY,
    name text
);

CREATE TABLE legacy_import_fixture.surveys_questions (
    id bigint PRIMARY KEY,
    question_id bigint,
    survey_id bigint,
    date_begin date,
    date_end date
);

CREATE TABLE legacy_import_fixture.access_to_survey (
    id bigint PRIMARY KEY,
    survey_id bigint,
    io_id bigint,
    begin_date date,
    end_date date
);

CREATE TABLE legacy_import_fixture.csp (
    id bigint PRIMARY KEY,
    file_name_survey text,
    survey bytea,
    file_name_csp text,
    csp bytea
);

CREATE TABLE legacy_import_fixture.survey_response (
    id bigint PRIMARY KEY,
    io_id bigint,
    survey_id bigint,
    date date,
    id_csp bigint
);

CREATE TABLE legacy_import_fixture.user_answers (
    id bigint PRIMARY KEY,
    question_id bigint,
    survey_response_id bigint,
    answer text,
    comment text
);

INSERT INTO legacy_import_fixture.organisation
VALUES
    (100, 'Legacy historical organization', 'Old street', '1'),
    (101, 'Legacy import test organization', 'Test street', '10');

INSERT INTO legacy_import_fixture.roles
VALUES
    (1, 'Пользователь'),
    (2, 'Администратор');

INSERT INTO legacy_import_fixture.users
VALUES (
    201,
    'legacy-import-test-user',
    'legacy-plain-text-password',
    1,
    'Тестов',
    'Легаси',
    'Импортович',
    'legacy-import@example.test',
    DATE '2026-01-01',
    DATE '2027-12-31'
), (
    202,
    'legacy-import-admin',
    'legacy-admin-plain-text-password',
    2,
    'Администратор',
    'Без',
    'Организации',
    NULL,
    DATE '2026-01-01',
    NULL
);

INSERT INTO legacy_import_fixture.io
VALUES
    (300, 201, 100, DATE '2025-01-01', DATE '2026-01-01'),
    (301, 201, 101, DATE '2026-01-01', DATE '2027-12-31');

INSERT INTO legacy_import_fixture.surveys
VALUES
    (
        401,
        'Legacy import test survey',
        DATE '2001-01-01',
        DATE '2001-01-31',
        'false',
        0,
        'Legacy import fixture'
    ),
    (
        402,
        'Legacy new survey',
        DATE '2002-02-01',
        DATE '2002-02-28',
        'false',
        0,
        'Legacy new survey fixture'
    );

INSERT INTO legacy_import_fixture.questions
VALUES
    (501, 'Legacy server question one'),
    (502, 'Legacy server question two'),
    (503, 'Legacy new survey question');

INSERT INTO legacy_import_fixture.surveys_questions
VALUES
    (601, 501, 401, DATE '2026-08-01', DATE '2026-08-20'),
    (602, 502, 401, DATE '2026-08-01', DATE '2026-08-20'),
    (603, 501, 401, DATE '2026-08-01', DATE '2026-08-20'),
    (604, 503, 402, DATE '2026-09-01', DATE '2026-09-20');

INSERT INTO legacy_import_fixture.access_to_survey
VALUES
    (701, 401, 301, DATE '2026-08-01', DATE '2026-08-20'),
    (702, 402, 301, DATE '2026-09-01', DATE '2026-09-20');

INSERT INTO legacy_import_fixture.csp
VALUES (
    801,
    'legacy-survey.docx',
    convert_to('legacy signed document', 'UTF8'),
    'legacy-signature.sig',
    convert_to('legacy signature', 'UTF8')
);

INSERT INTO legacy_import_fixture.survey_response
VALUES
    (901, 301, 401, DATE '2026-08-10', 801),
    (902, 300, 402, DATE '2026-09-10', NULL);

INSERT INTO legacy_import_fixture.user_answers
VALUES
    (1001, 501, 901, '5', 'This comment must be removed'),
    (1002, 502, 901, '4', 'Legacy comment'),
    (1003, 501, 901, '5', 'This comment must be removed'),
    (1004, 503, 902, '4', 'Historical response without retained access');

WITH existing_survey AS (
    INSERT INTO public.survey (
        name_survey,
        description,
        date_begin,
        date_end
    )
    VALUES (
        'Legacy import test survey',
        'Current survey description must be preserved',
        DATE '2026-08-01',
        DATE '2026-08-20'
    )
    RETURNING id_survey
)
INSERT INTO public.survey_question (id_survey, question_order, question_text)
SELECT id_survey, question_order, question_text
FROM existing_survey
CROSS JOIN (
    VALUES
        (1, 'Legacy server question one'),
        (2, 'Legacy server question two')
) question(question_order, question_text);

INSERT INTO public.organization (
    id_organization,
    organization_name,
    organization_short_name,
    date_begin,
    date_end
)
VALUES (
    1,
    'Current target organization',
    'Current target',
    DATE '2026-01-01',
    DATE '2027-01-01'
);

DO $$
DECLARE
    sequence_row record;
BEGIN
    FOR sequence_row IN
        SELECT sequence_namespace.nspname, sequence_relation.relname
        FROM pg_class sequence_relation
        INNER JOIN pg_namespace sequence_namespace
          ON sequence_namespace.oid = sequence_relation.relnamespace
        WHERE sequence_relation.relkind = 'S'
          AND sequence_namespace.nspname = 'public'
    LOOP
        PERFORM setval(
            format('%I.%I', sequence_row.nspname, sequence_row.relname)::regclass,
            1,
            false
        );
    END LOOP;
END
$$;
