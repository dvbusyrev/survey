import { expect, test } from '@playwright/test';

const password = 'SmokePass1!';

function localIsoDaysAgo(days) {
    const date = new Date();
    date.setDate(date.getDate() - days);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

function localDisplayDaysAgo(days) {
    const [year, month, day] = localIsoDaysAgo(days).split('-');
    return `${day}.${month}.${year}`;
}

async function login(page, loginName) {
    await page.goto('/');
    await page.locator('#username').fill(loginName);
    await page.locator('#password').fill(password);
    await page.getByRole('button', { name: 'Войти', exact: true }).click();
    await expect(page).toHaveURL(/\/survey$/);
}

async function expectPastEndDateToast(page) {
    const toast = page.locator('.site-toast--error')
        .filter({ hasText: 'Дата конца не может быть раньше сегодняшней даты.' })
        .last();
    await expect(toast).toBeVisible();
    await toast.locator('.site-toast__close').click();
}

test('обязательные поля входа перечисляются в верхнем уведомлении', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: 'Войти', exact: true }).click();

    const toast = page.locator('.site-toast--error').last();
    await expect(toast).toBeVisible();
    await expect(toast).toContainText('Введите логин.');
    await expect(toast).toContainText('Введите пароль.');
    await expect(page.locator('#username')).toHaveClass(/invalid/);
    await expect(page.locator('#password')).toHaveClass(/invalid/);
    await expect(page.locator('[data-role="field-error"]')).toHaveCount(0);
});

test('ошибка входа использует актуальное название логина', async ({ page }) => {
    await page.goto('/');
    await page.locator('#username').fill('smoke-admin');
    await page.locator('#password').fill('WrongPassword1!');
    await page.getByRole('button', { name: 'Войти', exact: true }).click();

    const toast = page.locator('.site-toast--error')
        .filter({ hasText: 'Неверный логин или пароль.' })
        .last();
    await expect(toast).toBeVisible();
    await expect(toast).not.toContainText('имя пользователя');
});

test('архивный пользователь и пользователь архивной организации не могут войти', async ({ page }) => {
    for (const loginName of ['smoke-archived-user', 'smoke-archived-org-user']) {
        await page.goto('/');
        await page.locator('#username').fill(loginName);
        await page.locator('#password').fill(password);
        await page.getByRole('button', { name: 'Войти', exact: true }).click();

        const toast = page.locator('.site-toast--error')
            .filter({ hasText: 'Пользователь заблокирован.' })
            .last();
        await expect(toast).toBeVisible();
        await expect(page).toHaveURL(/\/$/);
    }
});

test('администратор проходит основные разделы', async ({ page }) => {
    test.setTimeout(45_000);
    await login(page, 'smoke-admin');

    await expect(page.locator('[data-page="surveys-list"]')).toBeVisible();
    await page.locator('[data-role="survey-organization-filter-trigger"]').click();
    await expect(page.locator('[data-role="survey-organization-filter-popover"]')).not.toHaveClass(/is-hidden/);

    await page.locator('[data-click-call="openAddSurveyModal"]').click();
    await expect(page.locator('#surveyEditorModal')).toBeVisible();
    await expect(page.locator('#surveyEditorModal')).toContainText('Добавление анкеты');
    await page.locator('[data-role="survey-submit"]').click();
    const surveyRequiredToast = page.locator('.site-toast--error').last();
    await expect(surveyRequiredToast).toContainText('Введите название анкеты.');
    await expect(surveyRequiredToast).toContainText('Укажите дату начала.');
    await expect(surveyRequiredToast).toContainText('Укажите дату конца.');
    await expect(surveyRequiredToast).toContainText('Выберите хотя бы одну организацию.');
    await expect(surveyRequiredToast).toContainText('Добавьте хотя бы один критерий оценки.');
    await expect(surveyRequiredToast)
        .toBeVisible();
    await surveyRequiredToast.locator('.site-toast__close').click();
    await page.locator('#surveyTitle').fill('Анкета с просроченной датой');
    await expect(page.locator('#surveyTitle')).not.toHaveClass(/invalid/);
    await page.locator('#startDate').fill(localIsoDaysAgo(2));
    await page.locator('#endDate').fill(localIsoDaysAgo(1));
    await page.locator('[data-role="organization-dropdown-trigger"]').click();
    await page.locator('[data-role="organization-option"]').first().click();
    await page.locator('.criteriy').fill('Критерий');
    await page.locator('[data-role="survey-submit"]').click();
    await expectPastEndDateToast(page);
    await page.locator('#surveyEditorModal .modal-close').click();

    await page.getByRole('button', { name: 'Копировать', exact: true }).first().click();
    await expect(page.locator('#surveyEditorModal')).toBeVisible();
    await expect(page.locator('#surveyEditorModal')).toContainText('Копирование анкеты');
    await page.locator('#startDate').fill(localIsoDaysAgo(2));
    await page.locator('#endDate').fill(localIsoDaysAgo(1));
    await page.locator('[data-role="survey-submit"]').click();
    await expectPastEndDateToast(page);
    await page.locator('#surveyEditorModal .modal-close').click();

    await page.getByRole('link', { name: 'Редактировать', exact: true }).first().click();
    await expect(page.locator('#surveyEditorModal')).toBeVisible();
    await expect(page.locator('#surveyEditorModal')).toContainText('Редактирование анкеты');
    await page.locator('#startDate').fill(localIsoDaysAgo(2));
    await page.locator('#endDate').fill(localIsoDaysAgo(1));
    await page.locator('[data-role="survey-submit"]').click();
    await expectPastEndDateToast(page);
    await page.locator('#surveyTitle').fill('Smoke survey edited');
    await page.locator('#surveyDescription').fill('Edited survey description');
    await page.locator('#surveyEditorModal .criteriy').fill('Edited smoke question');
    await page.locator('#startDate').fill(localIsoDaysAgo(0));
    await page.locator('#endDate').fill(localIsoDaysAgo(-14));
    const updateResponsePromise = page.waitForResponse((response) => (
        response.request().method() === 'POST'
        && /\/survey\/\d+\/update$/.test(new URL(response.url()).pathname)
    ));
    await page.locator('[data-role="survey-submit"]').click();
    const updateResponse = await updateResponsePromise;
    expect(updateResponse.status()).toBe(200);
    await expect(page.locator('.site-toast--success').filter({ hasText: 'Анкета успешно обновлена.' }).last()).toBeVisible();
    await expect(page.locator('.surveys-table tbody')).toContainText('Smoke survey edited');
    await page.getByRole('link', { name: 'Редактировать', exact: true }).first().click();
    await expect(page.locator('#surveyEditorModal')).toBeVisible();
    await expect(page.locator('#surveyTitle')).toHaveValue('Smoke survey edited');
    await expect(page.locator('#surveyDescription')).toHaveValue('Edited survey description');
    await expect(page.locator('#surveyEditorModal .criteriy')).toHaveValue('Edited smoke question');
    await expect(page.locator('#startDate')).toHaveValue(localDisplayDaysAgo(0));
    await expect(page.locator('#endDate')).toHaveValue(localDisplayDaysAgo(-14));
    await page.locator('#surveyEditorModal .modal-close').click();

    await page.locator('a.nav-link[href="/users"]').click();
    await expect(page).toHaveURL(/\/users$/);
    await expect(page.locator('[data-page="users-list"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Добавить пользователя', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Добавить пользователя', exact: true }).click();
    await expect(page.locator('#addUserModal')).toBeVisible();
    await page.locator('#addUserModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    const userRequiredToast = page.locator('.site-toast--error').last();
    await expect(userRequiredToast).toContainText('Введите ФИО.');
    await expect(userRequiredToast).toContainText('Введите логин.');
    await expect(userRequiredToast).toContainText('Введите пароль.');
    await expect(userRequiredToast).toContainText('Выберите организацию.');
    await expect(userRequiredToast).toContainText('Укажите дату начала.');
    await userRequiredToast.locator('.site-toast__close').click();
    await page.locator('#fullName').fill('Повторный пользователь');
    await page.locator('#username').fill('smoke-admin');
    await page.locator('#password').fill('SmokePassword1!');
    await page.locator('#userOrganization').selectOption({ index: 1 });
    await page.locator('#userRole').selectOption('user');
    await page.locator('#dateBegin').fill(localIsoDaysAgo(0));
    await page.locator('#addUserModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    const duplicateCreateToast = page.locator('.site-toast--error')
        .filter({ hasText: 'Пользователь с таким логином существует.' })
        .last();
    await expect(duplicateCreateToast).toBeVisible();
    await duplicateCreateToast.locator('.site-toast__close').click();
    await page.locator('#fullName').fill('Просроченный пользователь');
    await page.locator('#username').fill('expired-user');
    await page.locator('#password').fill('SmokePassword1!');
    await page.locator('#userOrganization').selectOption({ index: 1 });
    await page.locator('#userRole').selectOption('user');
    await page.locator('#dateBegin').fill(localIsoDaysAgo(2));
    await page.locator('#dateEnd').fill(localIsoDaysAgo(1));
    await page.locator('#addUserModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    await expectPastEndDateToast(page);
    await page.locator('#addUserModal .modal-close').click();

    await page.locator('[data-role="user-row"][data-user-name="smoke-client"]')
        .locator('[data-click-call="openEditUserModalFromTrigger"]')
        .click();
    await expect(page.locator('#editUserModal')).toBeVisible();
    await page.locator('#editUsername').fill('smoke-admin');
    await page.locator('#editUserModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    const duplicateUpdateToast = page.locator('.site-toast--error')
        .filter({ hasText: 'Пользователь с таким логином существует.' })
        .last();
    await expect(duplicateUpdateToast).toBeVisible();
    await duplicateUpdateToast.locator('.site-toast__close').click();
    await page.locator('#editUsername').fill('smoke-client');
    await page.locator('#editDateBegin').fill(localIsoDaysAgo(2));
    await page.locator('#editDateEnd').fill(localIsoDaysAgo(1));
    await page.locator('#editUserModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    await expectPastEndDateToast(page);
    await page.locator('#editUserModal .modal-close').click();

    await page.goto('/organizations');
    await expect(page.locator('[data-page="organization-list"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Добавить организацию', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Добавить организацию', exact: true }).click();
    await expect(page.locator('#addOrganizationModal')).toBeVisible();
    await page.locator('#addOrganizationModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    const organizationRequiredToast = page.locator('.site-toast--error').last();
    await expect(organizationRequiredToast).toContainText('Введите название организации.');
    await expect(organizationRequiredToast).toContainText('Укажите дату начала.');
    await organizationRequiredToast.locator('.site-toast__close').click();
    await page.locator('#Name').fill('Просроченная организация');
    await page.locator('#DateBegin').fill(localIsoDaysAgo(2));
    await page.locator('#DateEnd').fill(localIsoDaysAgo(1));
    await page.locator('#addOrganizationModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    await expectPastEndDateToast(page);
    await page.locator('#addOrganizationModal .modal-close').click();

    await page.locator('[data-click-call="openEditOrganizationModalFromTrigger"]').first().click();
    await expect(page.locator('#editOrganizationModal')).toBeVisible();
    await page.locator('#organizationDateBegin').fill(localIsoDaysAgo(2));
    await page.locator('#organizationDateEnd').fill(localIsoDaysAgo(1));
    await page.locator('#editOrganizationModal').getByRole('button', { name: 'Сохранить', exact: true }).click();
    await expectPastEndDateToast(page);
    await page.locator('#editOrganizationModal .modal-close').click();

    await page.goto('/email');
    await expect(page.locator('[data-page="mail-compose"]')).toBeVisible();
    await page.locator('#email-save-button').click();
    const emailRequiredToast = page.locator('.site-toast--error').last();
    await expect(emailRequiredToast).toContainText('Укажите хотя бы одну эл. почту получателя.');
    await expect(emailRequiredToast).toContainText('Введите тему письма.');
    await expect(emailRequiredToast).toContainText('Введите текст письма.');

    await page.goto('/settings/survey-creation');
    await expect(page.locator('[data-page="survey-auto-creation"]')).toBeVisible();
    await page.locator('#surveyAutoCreationReportingOffset').fill('');
    await page.locator('#surveyAutoCreationActivePeriod').fill('');
    await page.getByRole('button', { name: 'Сохранить', exact: true }).click();
    const autoCreationRequiredToast = page.locator('.site-toast--error').last();
    await expect(autoCreationRequiredToast).toContainText('Введите срок подготовки отчёта.');
    await expect(autoCreationRequiredToast).toContainText('Введите срок доступности анкет.');

    await page.goto('/logs');
    await expect(page.locator('[data-page="get_logs"]')).toBeVisible();
    await expect(page.locator('#logs-table-top')).toBeVisible();

    await page.goto('/settings/theme');
    await expect(page.locator('[data-page="theme-settings-page"]')).toBeVisible();
    await expect(page.getByText('Настройка', { exact: true })).toBeVisible();
    await expect(page.getByText('Как выглядит', { exact: true })).toBeVisible();
});

test('ошибка удаления сохраняет текущий список и объясняет причину', async ({ page }) => {
    await login(page, 'smoke-admin');

    const surveyUrl = page.url();
    await page.route(/\/survey\/\d+\/delete$/, async (route) => {
        await route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify({
                success: false,
                message: 'Нельзя удалить анкету "Smoke survey": она уже назначалась организациям.'
            })
        });
    });
    await page.locator('[data-click-call="deleteSurveyFromTrigger"]').first().click();
    await page.locator('.site-confirm__button--confirm').click();
    const surveyToast = page.locator('.site-toast--error')
        .filter({ hasText: 'Нельзя удалить анкету "Smoke survey"' })
        .last();
    await expect(surveyToast).toBeVisible();
    await expect(page).toHaveURL(surveyUrl);
    await surveyToast.locator('.site-toast__close').click();

    await page.goto('/users');
    const usersUrl = page.url();
    await page.route(/\/users\/\d+\/delete$/, async (route) => {
        await route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify({
                success: false,
                message: 'Нельзя удалить пользователя "Smoke client": он связан с сохранёнными ответами или черновиками анкет.'
            })
        });
    });
    await page.locator('[data-role="user-row"][data-user-name="smoke-client"]')
        .locator('[data-click-call="deleteUserFromTrigger"]')
        .click();
    await page.locator('.site-confirm__button--confirm').click();
    const userToast = page.locator('.site-toast--error')
        .filter({ hasText: 'Нельзя удалить пользователя "Smoke client"' })
        .last();
    await expect(userToast).toBeVisible();
    await expect(page).toHaveURL(usersUrl);
    await userToast.locator('.site-toast__close').click();

    await page.goto('/organizations');
    const organizationsUrl = page.url();
    await page.route(/\/organizations\/\d+\/delete$/, async (route) => {
        await route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify({
                success: false,
                message: 'Нельзя удалить организацию: для неё уже заводились анкеты и выбирались пользователи.'
            })
        });
    });
    await page.locator('[data-click-call="deleteOrganization"]').first().click();
    await page.locator('.site-confirm__button--confirm').click();
    const organizationToast = page.locator('.site-toast--error')
        .filter({ hasText: 'Нельзя удалить организацию' })
        .last();
    await expect(organizationToast).toBeVisible();
    await expect(page).toHaveURL(organizationsUrl);
});

test('длинная таблица критериев прокручивается вместе с модальным окном', async ({ page }) => {
    await page.setViewportSize({ width: 947, height: 975 });
    await login(page, 'smoke-admin');
    await page.route(/\/survey\/\d+\/details$/, async (route) => {
        await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
                name: 'Анкета с длинным списком критериев',
                description: 'Проверка прокрутки окна просмотра анкеты',
                dateBegin: localDisplayDaysAgo(0),
                dateEnd: localDisplayDaysAgo(-14),
                organizations: ['Smoke org'],
                criteria: Array.from({ length: 24 }, (_, index) => `Критерий ${index + 1}`)
            })
        });
    });

    await page.locator('.surveys-table tbody tr[data-survey-id]').first().locator('td').first().click();
    const modal = page.locator('.survey-details-modal.modal--visible');
    const modalBody = modal.locator('.survey-details-modal__body');
    const tableWrap = modal.locator('.survey-details-modal__table-wrap');
    await expect(modal).toBeVisible();

    const scrollState = await modal.evaluate((modalElement) => {
        const body = modalElement.querySelector('.survey-details-modal__body');
        const wrap = modalElement.querySelector('.survey-details-modal__table-wrap');
        return {
            bodyClientHeight: body.clientHeight,
            bodyScrollHeight: body.scrollHeight,
            bodyOverflowY: getComputedStyle(body).overflowY,
            wrapClientHeight: wrap.clientHeight,
            wrapScrollHeight: wrap.scrollHeight,
            wrapOverflowY: getComputedStyle(wrap).overflowY
        };
    });

    expect(scrollState.bodyOverflowY).toBe('auto');
    expect(scrollState.bodyScrollHeight).toBeGreaterThan(scrollState.bodyClientHeight);
    expect(scrollState.wrapOverflowY).toBe('visible');
    expect(scrollState.wrapScrollHeight).toBe(scrollState.wrapClientHeight);

    await modalBody.evaluate((element) => {
        element.scrollTop = element.scrollHeight;
    });
    await expect(tableWrap.locator('tbody tr').last()).toBeInViewport();
});

test('клиент видит ошибку, если срок анкеты истёк перед отправкой', async ({ page }) => {
    await login(page, 'smoke-client');

    const surveyRow = page.locator('[data-role="user-survey-row"][data-row-action="fill"]');
    await expect(surveyRow).toHaveCount(1);
    await surveyRow.click();

    const fillPage = page.locator('[data-role="survey-fill-page"]');
    await expect(fillPage).toBeVisible();
    const questions = fillPage.locator('[data-role="survey-question"]');
    for (let index = 0; index < await questions.count(); index += 1) {
        await questions.nth(index).locator('[data-role="rating-button"][data-rating="5"]').click();
    }

    await page.route('**/answers/create', async (route) => {
        await route.fulfill({
            status: 409,
            contentType: 'application/json',
            body: JSON.stringify({
                error: 'Срок прохождения анкеты истёк. Ответы не отправлены.'
            })
        });
    });

    await page.getByRole('button', { name: 'Отправить ответы', exact: true }).click();

    await expect(page.locator('.site-toast--error')
        .filter({ hasText: 'Срок прохождения анкеты истёк. Ответы не отправлены.' })
        .last()).toBeVisible();
    await expect(fillPage).toBeVisible();
});

test('клиент проходит доступные анкеты, черновик, отправку, архив и справку', async ({ page }) => {
    test.setTimeout(45_000);
    await login(page, 'smoke-client');

    const surveyRow = page.locator('[data-role="user-survey-row"][data-row-action="fill"]');
    await expect(surveyRow).toHaveCount(1);
    await surveyRow.click();

    const fillPage = page.locator('[data-role="survey-fill-page"]');
    await expect(fillPage).toBeVisible();
    await expect(page.getByRole('button', { name: 'Подписать', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Отправить ответы', exact: true }).click();
    const answerRequiredToast = page.locator('.site-toast--error').last();
    await expect(answerRequiredToast).toContainText('Выберите оценку для каждого вопроса.');
    await expect(fillPage.locator('[data-role="ratings"]').first()).toHaveClass(/invalid/);

    const firstQuestion = fillPage.locator('[data-role="survey-question"]').first();
    const commentBlock = firstQuestion.locator('[data-role="comment-block"]');
    const commentInput = firstQuestion.locator('[data-role="comment-input"]');
    await expect(commentBlock).toBeHidden();

    await firstQuestion.locator('[data-role="rating-button"][data-rating="4"]').click();
    await expect(commentBlock).toBeVisible();
    await commentInput.fill('Комментарий для оценки 4');

    const draftSaved = page.waitForResponse((response) =>
        response.url().endsWith('/answers/draft')
        && response.request().method() === 'POST'
        && response.status() === 200);
    await firstQuestion.locator('[data-role="rating-button"][data-rating="5"]').click();
    await draftSaved;
    await expect(commentBlock).toBeHidden();
    await expect(commentInput).toHaveValue('');

    const answerSaved = page.waitForResponse((response) =>
        response.url().endsWith('/answers/create')
        && response.request().method() === 'POST'
        && response.status() === 200);
    await page.getByRole('button', { name: 'Отправить ответы', exact: true }).click();
    await answerSaved;
    await expect(page.locator('.site-toast--success')
        .filter({
            hasText: 'Ответы на анкету успешно отправлены. Анкета перенесена в раздел «Архив анкет».'
        })
        .last()).toBeVisible();

    await page.goto('/archive');
    await expect(page.locator('[data-role="survey-user-content"][data-active-tab="archived"]')).toBeVisible();
    const archivedSurveyRow = page.locator('[data-role="user-survey-row"][data-row-action="view"]');
    await expect(archivedSurveyRow).toHaveCount(1);
    await archivedSurveyRow.click();
    await expect(page.locator('[data-role="survey-answers-page"]')).toBeVisible();

    await page.evaluate(() => {
        const certificate = {
            SubjectName: 'CN=Тестовый подписант',
            IssuerName: 'CN=Тестовый удостоверяющий центр',
            ValidFromDate: '2026-01-01T00:00:00Z',
            ValidToDate: '2030-01-01T00:00:00Z',
            Thumbprint: 'TEST-THUMBPRINT'
        };
        const certificates = {
            Count: 1,
            Item: async () => certificate
        };

        window.cadesplugin = {
            version: 'test',
            CreateObjectAsync: async (name) => {
                if (name === 'CAdESCOM.Store') {
                    return {
                        Open: async () => {},
                        Certificates: certificates
                    };
                }
                if (name === 'CAdESCOM.CPSigner') {
                    return { propset_Certificate: async () => {} };
                }
                if (name === 'CAdESCOM.CadesSignedData') {
                    return {
                        propset_ContentEncoding: async () => {},
                        propset_Content: async () => {},
                        SignCades: async () => 'test-signature'
                    };
                }

                return {};
            }
        };
    });

    let rejectSignature = true;
    await page.route('**/signatures/*/*', async (route) => {
        if (route.request().method() === 'GET') {
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify({
                    content: '0KLQtdGB0YI=',
                    contentEncoding: 'base64',
                    detached: true
                })
            });
            return;
        }

        await route.fulfill({
            status: rejectSignature ? 500 : 200,
            contentType: 'application/json',
            body: JSON.stringify(rejectSignature
                ? { error: 'Не удалось подписать анкету.' }
                : { success: true, message: 'Анкета успешно подписана.' })
        });
    });

    const signButton = page.getByRole('button', { name: 'Подписать', exact: true });
    await signButton.click();
    await page.locator('.cert-item').click();
    await expect(page.locator('.site-toast--error')
        .filter({ hasText: 'Не удалось подписать анкету.' })
        .last()).toBeVisible();

    rejectSignature = false;
    await signButton.click();
    await page.locator('.cert-item').click();
    await expect(page.locator('.site-toast--success')
        .filter({ hasText: 'Анкета успешно подписана.' })
        .last()).toBeVisible();

    await page.goto('/help');
    await expect(page.locator('[data-role="survey-user-content"][data-active-tab="help"]')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Скачать инструкцию', exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Выйти', exact: true }).click();
    await expect(page).toHaveURL(/\/$/);
    await login(page, 'smoke-admin');
    await page.goto('/survey/answer');
    const answerJournalRow = page.locator('.answers-page__row').first();
    await expect(answerJournalRow).toBeVisible();
    await answerJournalRow.click();
    await expect(page.locator('#answersModal')).toBeVisible();
    await expect(page.locator('#answersContainer .answers-modal__table tbody tr')).toHaveCount(1);
    await expect(page.locator('#answersContainer .answers-modal__table-wrap')).toHaveCSS('padding-bottom', '0px');
});

test('клиент скачивает установленную инструкцию', async ({ page }) => {
    await login(page, 'smoke-client');
    await page.goto('/help');
    await expect(page.getByRole('link', { name: 'Скачать инструкцию', exact: true })).toBeVisible();

    const helpDownloadResponse = await page.evaluate(async () => {
        const response = await fetch('/help/download/user-guide');
        const body = await response.arrayBuffer();
        return {
            status: response.status,
            contentType: response.headers.get('content-type') || '',
            bodyLength: body.byteLength
        };
    });
    expect(helpDownloadResponse.status).toBe(200);
    expect(helpDownloadResponse.contentType).toContain(
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document');
    expect(helpDownloadResponse.bodyLength).toBeGreaterThan(0);
});
