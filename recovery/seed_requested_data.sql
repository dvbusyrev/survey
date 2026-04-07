BEGIN;

-- Remove the temporary bootstrap data created during recovery.
DELETE FROM public.history_answer
WHERE id_survey IN (
    SELECT id_survey
    FROM public.surveys
    WHERE name_survey = 'Анкета МКУ'
);

DELETE FROM public.access_extensions
WHERE id_survey IN (
    SELECT id_survey
    FROM public.surveys
    WHERE name_survey = 'Анкета МКУ'
);

DELETE FROM public.omsu_surveys
WHERE id_survey IN (
    SELECT id_survey
    FROM public.surveys
    WHERE name_survey = 'Анкета МКУ'
);

DELETE FROM public.surveys
WHERE name_survey = 'Анкета МКУ';

DELETE FROM public.users
WHERE name_user IN ('admin', 'Гордеев_СВ', 'мку1');

DELETE FROM public.omsu
WHERE name_omsu IN ('Администрирование', 'МКУ');

INSERT INTO public.omsu (
    name_omsu,
    date_begin,
    date_end,
    block,
    email
)
VALUES
    ('Администрирование', CURRENT_DATE, NULL, false, NULL),
    ('МКУ', CURRENT_DATE, NULL, false, NULL);

WITH admin_org AS (
    SELECT id_omsu
    FROM public.omsu
    WHERE name_omsu = 'Администрирование'
    ORDER BY id_omsu DESC
    LIMIT 1
),
user_org AS (
    SELECT id_omsu
    FROM public.omsu
    WHERE name_omsu = 'МКУ'
    ORDER BY id_omsu DESC
    LIMIT 1
)
INSERT INTO public.users (
    id_omsu,
    name_user,
    full_name,
    name_role,
    hash_password,
    email,
    date_begin,
    date_end
)
VALUES
    (
        (SELECT id_omsu FROM admin_org),
        'Гордеев_СВ',
        'Гордеев_СВ',
        'admin',
        'ww3JowX+rRT3iSa6OTHiCX+QflwFcdRzGOFguIkZm2X9LhTkV0JDX9sVFkUy2YiseuLaDb1nh5QY9N2S8OlegA==',
        NULL,
        NOW(),
        NULL
    ),
    (
        (SELECT id_omsu FROM user_org),
        'мку1',
        'мку1',
        'user',
        'PJkJr+wlNU1VHa4hWQuybjjVPyFzuNPcPu5MBH56scHri4UQPjvnumE7MbtcnDYhTcnxSkL9ei/bhIVrylxEwg==',
        NULL,
        NOW(),
        NULL
    );

WITH mku_org AS (
    SELECT id_omsu
    FROM public.omsu
    WHERE name_omsu = 'МКУ'
    ORDER BY id_omsu DESC
    LIMIT 1
),
new_survey AS (
    INSERT INTO public.surveys (
        name_survey,
        description,
        date_create,
        date_open,
        date_close
    )
    VALUES (
        'Анкета МКУ',
        'Восстановленная анкета для организации МКУ',
        NOW(),
        NOW() - interval '1 day',
        NOW() + interval '30 days'
    )
    RETURNING id_survey, date_create
),
insert_questions AS (
    INSERT INTO public.survey_questions (id_survey, question_order, question_text)
    SELECT
        ns.id_survey,
        question_data.question_order,
        question_data.question_text
    FROM new_survey ns
    CROSS JOIN (
        VALUES
            (1, 'Критерий 1'),
            (2, 'Критерий 2'),
            (3, 'Критерий 3')
    ) AS question_data(question_order, question_text)
),
link_mku AS (
    INSERT INTO public.omsu_surveys (id_omsu, id_survey)
    SELECT
        (SELECT id_omsu FROM mku_org),
        (SELECT id_survey FROM new_survey)
),
new_answer AS (
    INSERT INTO public.history_answer (
        id_omsu,
        id_survey,
        csp,
        completion_date,
        create_date_survey
    )
    SELECT
        (SELECT id_omsu FROM mku_org),
        ns.id_survey,
        NULL,
        NOW(),
        ns.date_create
    FROM new_survey ns
    RETURNING id_answer
)
INSERT INTO public.history_answer_items (
    id_answer,
    question_order,
    question_text,
    rating,
    comment
)
SELECT
    na.id_answer,
    answer_data.question_order,
    answer_data.question_text,
    4,
    answer_data.comment
FROM new_answer na
CROSS JOIN (
    VALUES
        (1, 'Критерий 1', '1'),
        (2, 'Критерий 2', '2'),
        (3, 'Критерий 3', '3')
) AS answer_data(question_order, question_text, comment);

COMMIT;
