import { pbkdf2Sync, randomBytes } from 'node:crypto';

function identityPasswordHash(password) {
    const salt = randomBytes(16);
    const subkey = pbkdf2Sync(password, salt, 100_000, 32, 'sha512');
    const payload = Buffer.alloc(13 + salt.length + subkey.length);

    payload[0] = 0x01;
    payload.writeUInt32BE(2, 1);
    payload.writeUInt32BE(100_000, 5);
    payload.writeUInt32BE(salt.length, 9);
    salt.copy(payload, 13);
    subkey.copy(payload, 13 + salt.length);
    return payload.toString('base64');
}

const passwordHash = identityPasswordHash('SmokePass1!');

process.stdout.write(`\\set ON_ERROR_STOP on

INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
VALUES ('Smoke organization', 'Smoke org', CURRENT_DATE)
RETURNING id_organization AS smoke_organization_id \\gset

INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
VALUES ('Smoke unrelated organization', 'Smoke unrelated org', CURRENT_DATE);

INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin)
VALUES
    (:smoke_organization_id, 'smoke-admin', 'Smoke administrator', 'admin', '${passwordHash}', 'admin@example.test', CURRENT_DATE),
    (:smoke_organization_id, 'smoke-client', 'Smoke client', 'user', '${passwordHash}', 'client@example.test', CURRENT_DATE);

INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
SELECT
    :smoke_organization_id,
    'smoke-pagination-' || series_number,
    'ZZZ Pagination user ' || LPAD(series_number::text, 2, '0'),
    'user',
    '${passwordHash}',
    CURRENT_DATE
FROM generate_series(1, 21) AS series_number;

INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin, date_end)
VALUES
    (:smoke_organization_id, 'smoke-archived-user', 'Smoke archived user', 'user', '${passwordHash}', 'archived-user@example.test', CURRENT_DATE - 10, CURRENT_DATE - 1);

INSERT INTO public.organization (organization_name, organization_short_name, date_begin, date_end)
VALUES ('Smoke archived organization', 'Smoke archived org', CURRENT_DATE - 10, CURRENT_DATE - 1)
RETURNING id_organization AS smoke_archived_organization_id \\gset

INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin)
VALUES
    (:smoke_archived_organization_id, 'smoke-archived-org-user', 'Smoke archived organization user', 'user', '${passwordHash}', 'archived-org-user@example.test', CURRENT_DATE - 10);

INSERT INTO public.survey (name_survey, description, date_begin, date_end)
VALUES ('Smoke survey', 'Survey used only by browser smoke tests', CURRENT_DATE - 1, CURRENT_DATE + 14)
RETURNING id_survey AS smoke_survey_id \\gset

INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
VALUES (:smoke_organization_id, :smoke_survey_id, CURRENT_DATE - 1, CURRENT_DATE + 30);

INSERT INTO public.survey (name_survey, description, date_begin, date_end)
VALUES (
    'Smoke archived extension survey',
    'Archived survey with a separate organization extension',
    CURRENT_DATE - 60,
    CURRENT_DATE - 40
)
RETURNING id_survey AS smoke_archived_extension_survey_id \\gset

INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
VALUES (
    :smoke_archived_organization_id,
    :smoke_archived_extension_survey_id,
    CURRENT_DATE - 60,
    CURRENT_DATE - 30
);

INSERT INTO public.survey_question (id_survey, question_order, question_text)
VALUES (:smoke_survey_id, 1, 'Smoke question');

INSERT INTO public.survey_template (name_survey_template, description, date_begin, date_end)
VALUES (
    'Smoke active template',
    'Active template used only by browser smoke tests',
    CURRENT_DATE - 1,
    CURRENT_DATE + 14
)
RETURNING id_survey_template AS smoke_active_template_id \\gset

INSERT INTO public.organization_survey_template (id_organization, id_survey_template)
VALUES (:smoke_organization_id, :smoke_active_template_id);

INSERT INTO public.survey_template_question (id_survey_template, question_order, question_text)
VALUES (:smoke_active_template_id, 1, 'Smoke template question');

INSERT INTO public.auto_creation_config (
    id_config,
    reporting_period,
    reporting_offset_business_days,
    working_period,
    is_enabled
)
VALUES (1, 'month', 1, 8, FALSE)
ON CONFLICT (id_config) DO UPDATE SET is_enabled = FALSE;

INSERT INTO public.survey_template_auto_creation_config (id_config, id_survey_template)
VALUES (1, :smoke_active_template_id);

INSERT INTO public.survey_template (name_survey_template, description, date_begin, date_end)
VALUES (
    'Smoke archived template',
    'Archived template used only by browser smoke tests',
    CURRENT_DATE - 30,
    CURRENT_DATE - 1
)
RETURNING id_survey_template AS smoke_archived_template_id \\gset

INSERT INTO public.organization_survey_template (id_organization, id_survey_template)
VALUES (:smoke_organization_id, :smoke_archived_template_id);

INSERT INTO public.survey_template_question (id_survey_template, question_order, question_text)
VALUES (:smoke_archived_template_id, 1, 'Smoke archived template question');
`);
