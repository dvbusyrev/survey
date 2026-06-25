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

INSERT INTO public.app_user (id_organization, login, full_name, role, password, email, date_begin)
VALUES
    (:smoke_organization_id, 'smoke-admin', 'Smoke administrator', 'admin', '${passwordHash}', 'admin@example.test', CURRENT_DATE),
    (:smoke_organization_id, 'smoke-client', 'Smoke client', 'user', '${passwordHash}', 'client@example.test', CURRENT_DATE);

INSERT INTO public.survey (name_survey, description)
VALUES ('Smoke survey', 'Survey used only by browser smoke tests')
RETURNING id_survey AS smoke_survey_id \\gset

INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
VALUES (:smoke_organization_id, :smoke_survey_id, CURRENT_DATE - 1, CURRENT_DATE + 14);

INSERT INTO public.survey_question (id_survey, question_order, question_text)
VALUES (:smoke_survey_id, 1, 'Smoke question');
`);
