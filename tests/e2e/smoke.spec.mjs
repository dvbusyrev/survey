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

    await page.goto('/archive');
    await expect(page.locator('[data-role="survey-user-content"][data-active-tab="archived"]')).toBeVisible();
    await expect(page.locator('[data-role="user-survey-row"][data-row-action="view"]')).toHaveCount(1);

    await page.goto('/help');
    await expect(page.locator('[data-role="survey-user-content"][data-active-tab="help"]')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Скачать инструкцию', exact: true })).toBeVisible();
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
