import { expect, test } from '@playwright/test';

const password = 'SmokePass1!';

async function login(page, loginName) {
    await page.goto('/');
    await page.locator('#username').fill(loginName);
    await page.locator('#password').fill(password);
    await page.getByRole('button', { name: 'Войти', exact: true }).click();
    await expect(page).toHaveURL(/\/survey$/);
}

test('администратор проходит основные разделы', async ({ page }) => {
    await login(page, 'smoke-admin');

    await expect(page.locator('[data-page="surveys-list"]')).toBeVisible();
    await page.locator('[data-role="survey-organization-filter-trigger"]').click();
    await expect(page.locator('[data-role="survey-organization-filter-popover"]')).not.toHaveClass(/is-hidden/);

    await page.locator('[data-click-call="openAddSurveyModal"]').click();
    await expect(page.locator('#surveyEditorModal')).toBeVisible();
    await expect(page.locator('#surveyEditorModal')).toContainText('Добавление анкеты');
    await page.locator('#surveyEditorModal .modal-close').click();

    await page.locator('a.nav-link[href="/users"]').click();
    await expect(page).toHaveURL(/\/users$/);
    await expect(page.locator('[data-page="users-list"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Добавить пользователя', exact: true })).toBeVisible();

    await page.goto('/organizations');
    await expect(page.locator('[data-page="organization-list"]')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Добавить организацию', exact: true })).toBeVisible();

    await page.goto('/logs');
    await expect(page.locator('[data-page="get_logs"]')).toBeVisible();
    await expect(page.locator('#logs-table-top')).toBeVisible();

    await page.goto('/settings/theme');
    await expect(page.locator('[data-page="theme-settings-page"]')).toBeVisible();
    await expect(page.getByText('Настройка', { exact: true })).toBeVisible();
    await expect(page.getByText('Как выглядит', { exact: true })).toBeVisible();
});

test('клиент проходит доступные анкеты, черновик, отправку, архив и справку', async ({ page }) => {
    await login(page, 'smoke-client');

    const surveyRow = page.locator('[data-role="user-survey-row"][data-row-action="fill"]');
    await expect(surveyRow).toHaveCount(1);
    await surveyRow.click();

    const fillPage = page.locator('[data-role="survey-fill-page"]');
    await expect(fillPage).toBeVisible();
    await expect(page.getByRole('button', { name: 'Подписать', exact: true })).toBeVisible();

    const draftSaved = page.waitForResponse((response) =>
        response.url().endsWith('/answers/draft')
        && response.request().method() === 'POST'
        && response.status() === 200);
    await fillPage.locator('[data-role="rating-button"][data-rating="5"]').click();
    await draftSaved;

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
