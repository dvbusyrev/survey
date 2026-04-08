BEGIN;

-- Remove the temporary bootstrap data created during recovery.
DELETE FROM public.answer
WHERE id_survey IN (
    SELECT id_survey
    FROM public.survey
    WHERE name_survey = 'Анкета МКУ'
);

DELETE FROM public.organization_survey
WHERE id_survey IN (
    SELECT id_survey
    FROM public.survey
    WHERE name_survey = 'Анкета МКУ'
);

DELETE FROM public.survey
WHERE name_survey = 'Анкета МКУ';

DELETE FROM public.app_user
WHERE name_user IN ('admin', 'Гордеев_СВ', 'мку1');

DELETE FROM public.organization
WHERE organization_name IN ('Администрирование', 'МКУ');

INSERT INTO public.organization (
    organization_name,
    date_begin,
    date_end,
    block,
    email
)
VALUES
    ('Администрирование', CURRENT_DATE, NULL, false, NULL),
    ('МКУ', CURRENT_DATE, NULL, false, NULL);

WITH admin_org AS (
    SELECT organization_id
    FROM public.organization
    WHERE organization_name = 'Администрирование'
    ORDER BY organization_id DESC
    LIMIT 1
),
user_org AS (
    SELECT organization_id
    FROM public.organization
    WHERE organization_name = 'МКУ'
    ORDER BY organization_id DESC
    LIMIT 1
)
INSERT INTO public.app_user (
    organization_id,
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
        (SELECT organization_id FROM admin_org),
        'Гордеев_СВ',
        'Гордеев_СВ',
        'admin',
        'AQAAAAIAAYagAAAAEIxEcIcuk6eEmtW66OYBgzV99+ICAodnrZN2SbsAGbbeOKIpBbmxQsJvNfo3QCREKw==',
        NULL,
        NOW(),
        NULL
    ),
    (
        (SELECT organization_id FROM user_org),
        'мку1',
        'мку1',
        'user',
        'AQAAAAIAAYagAAAAEDjGnSSgzkgXPPVzrh2bJd7GLKCisktA8GIdQk0N6rdpWFJ2OgUc9sgxUZRfoEl3eA==',
        NULL,
        NOW(),
        NULL
    );

WITH mku_org AS (
    SELECT organization_id
    FROM public.organization
    WHERE organization_name = 'МКУ'
    ORDER BY organization_id DESC
    LIMIT 1
),
new_survey AS (
    INSERT INTO public.survey (
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
    INSERT INTO public.survey_question (id_survey, question_order, question_text)
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
    INSERT INTO public.organization_survey (organization_id, id_survey)
    SELECT
        (SELECT organization_id FROM mku_org),
        (SELECT id_survey FROM new_survey)
),
new_answer AS (
    INSERT INTO public.answer (
        organization_id,
        id_survey,
        csp,
        completion_date,
        create_date_survey
    )
    SELECT
        (SELECT organization_id FROM mku_org),
        ns.id_survey,
        NULL,
        NOW(),
        ns.date_create
    FROM new_survey ns
    RETURNING id_answer
)
INSERT INTO public.answer_item (
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
