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
    await page.waitForLoadState('load');
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

test('общий каркас виден до загрузки скриптов интерфейса', async ({ page }) => {
    await login(page, 'smoke-admin');
    await page.route(/\/js\/ui\/app-(header|navigation|footer)\.js(?:\?.*)?$/, async (route) => {
        await route.abort();
    });

    await page.goto('/users', { waitUntil: 'domcontentloaded' });

    const shell = page.locator('[data-app-shell="admin"]');
    const content = page.locator('#content_admin');
    await expect(shell).toHaveCount(1);
    await expect(page.locator('#chrome-header .app-header')).toBeVisible();
    await expect(page.locator('#chrome-navigation .admin-nav')).toBeVisible();
    await expect(content).toBeVisible();
    await expect(content).toHaveCSS('background-color', 'rgb(255, 255, 255)');
    await expect(page.locator('#chrome-footer footer')).toBeVisible();

    const beforeLoad = await content.boundingBox();
    await page.waitForLoadState('load');
    const afterLoad = await content.boundingBox();
    expect(beforeLoad).not.toBeNull();
    expect(afterLoad).not.toBeNull();
    expect(Math.abs(afterLoad.x - beforeLoad.x)).toBeLessThan(1);
    expect(Math.abs(afterLoad.y - beforeLoad.y)).toBeLessThan(1);
    expect(Math.abs(afterLoad.width - beforeLoad.width)).toBeLessThan(1);
    expect(Math.abs(afterLoad.height - beforeLoad.height)).toBeLessThan(1);
});

test('каркас переключается только между обычной и компактной версиями', async ({ page }) => {
    await page.setViewportSize({ width: 1221, height: 900 });
    await login(page, 'smoke-admin');

    await expect.poll(() => page.evaluate(() => ({
        rootFontSize: document.documentElement.style.getPropertyValue('--app-root-font-size'),
        compactClass: document.body.classList.contains('compact-nav-mode'),
        menuToggleDisplay: getComputedStyle(document.querySelector('.header-menu-toggle')).display,
        navigationPosition: getComputedStyle(document.querySelector('#chrome-navigation')).position
    }))).toEqual({
        rootFontSize: '149.25%',
        compactClass: false,
        menuToggleDisplay: 'none',
        navigationPosition: 'relative'
    });

    await page.setViewportSize({ width: 1220, height: 900 });

    await expect.poll(() => page.evaluate(() => ({
        rootFontSize: document.documentElement.style.getPropertyValue('--app-root-font-size'),
        compactClass: document.body.classList.contains('compact-nav-mode'),
        menuToggleDisplay: getComputedStyle(document.querySelector('.header-menu-toggle')).display,
        navigationPosition: getComputedStyle(document.querySelector('#chrome-navigation')).position
    }))).toEqual({
        rootFontSize: '118%',
        compactClass: true,
        menuToggleDisplay: 'flex',
        navigationPosition: 'fixed'
    });
});

test('уменьшение высоты окна не меняет версию навигации', async ({ page }) => {
    await page.setViewportSize({ width: 1221, height: 821 });
    await login(page, 'smoke-admin');

    const readNavigationLayout = () => page.evaluate(() => {
        const navigationLink = document.querySelector('.admin-nav .nav-link');
        const navigationStyle = getComputedStyle(navigationLink);
        const navigationHostStyle = getComputedStyle(document.querySelector('#chrome-navigation'));

        return {
            compactClass: document.body.classList.contains('compact-nav-mode'),
            navigationPosition: navigationHostStyle.position,
            paddingTop: navigationStyle.paddingTop,
            paddingBottom: navigationStyle.paddingBottom,
            rootFontSize: document.documentElement.style.getPropertyValue('--app-root-font-size')
        };
    });

    const regularHeightLayout = await readNavigationLayout();
    await page.setViewportSize({ width: 1221, height: 819 });
    await expect.poll(readNavigationLayout).toEqual(regularHeightLayout);
});

test('в настройках отправителя поле называется Пароль', async ({ page }) => {
    await login(page, 'smoke-admin');
    await page.goto('/settings/email');

    const passwordField = page.locator('#email-smtp-password');
    await expect(page.locator('label[for="email-smtp-password"]')).toHaveText('Пароль');
    await expect(passwordField).toHaveAttribute('placeholder', 'Введите пароль или оставьте поле пустым');
    await expect(page.getByText('Новый пароль', { exact: true })).toHaveCount(0);
});

test('подсказки строк и кнопок действий остаются у цели и в границах экрана', async ({ page }) => {
    await login(page, 'smoke-admin');
    await page.goto('/users');

    const row = page.locator('[data-role="user-row"][data-user-name="smoke-client"]');
    const rowBox = await row.boundingBox();
    expect(rowBox).not.toBeNull();

    await row.dispatchEvent('mouseover', {
        bubbles: true,
        clientX: page.viewportSize().width - 1,
        clientY: rowBox.y + (rowBox.height / 2)
    });

    const rowTooltip = page.locator('.app-row-tooltip');
    await expect(rowTooltip).toBeVisible();
    const rowTooltipBox = await rowTooltip.boundingBox();
    expect(rowTooltipBox).not.toBeNull();
    expect(rowTooltipBox.x).toBeGreaterThanOrEqual(8);
    expect(rowTooltipBox.x + rowTooltipBox.width).toBeLessThanOrEqual(page.viewportSize().width - 8);

    const deleteAction = row.locator('[data-click-call="deleteUserFromTrigger"]');
    await deleteAction.hover();
    await expect(rowTooltip).toBeHidden();

    const actionTooltip = deleteAction.locator('.icon-tooltip');
    await expect(actionTooltip).toBeVisible();
    await expect(actionTooltip).toHaveText('Удалить');
    await deleteAction.evaluate(() => new Promise((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(resolve));
    }));

    const tooltipGeometry = await deleteAction.evaluate((icon) => {
        const tooltip = icon.querySelector('.icon-tooltip');
        const iconRect = icon.getBoundingClientRect();
        const tooltipRect = tooltip.getBoundingClientRect();
        const arrowStyle = getComputedStyle(tooltip, '::after');
        return {
            iconCenter: iconRect.left + (iconRect.width / 2),
            tooltipLeft: tooltipRect.left,
            tooltipRight: tooltipRect.right,
            arrowCenter: tooltipRect.left + Number.parseFloat(arrowStyle.left)
        };
    });

    expect(tooltipGeometry.tooltipLeft).toBeGreaterThanOrEqual(8);
    expect(tooltipGeometry.tooltipRight).toBeLessThanOrEqual(page.viewportSize().width - 8);
    expect(Math.abs(tooltipGeometry.arrowCenter - tooltipGeometry.iconCenter)).toBeLessThanOrEqual(2);
});

test('администратор проходит основные разделы', async ({ page }) => {
    test.setTimeout(45_000);
    await login(page, 'smoke-admin');

    await expect(page.locator('[data-page="surveys-list"]')).toBeVisible();
    const initialSurveyRows = page.locator('[data-role="admin-survey-row"]');
    await expect(initialSurveyRows).toHaveCount(2);
    await expect(initialSurveyRows.nth(0)).toHaveAttribute('data-is-extension', 'false');
    await expect(initialSurveyRows.nth(1)).toHaveAttribute('data-is-extension', 'true');
    const activeExtensionRow = initialSurveyRows.filter({ hasText: 'Smoke survey: продление для Smoke org' });
    await expect(activeExtensionRow).toBeVisible();
    await expect(activeExtensionRow).toHaveAttribute('data-is-extension', 'true');
    await expect(activeExtensionRow.locator('[data-role="survey-extension-name"]'))
        .toHaveText(/^\s*↳\s*Smoke survey: продление для Smoke org\s*$/);
    await expect(activeExtensionRow.getByRole('link', { name: 'Проверить прохождение', exact: true })).toHaveCount(0);
    await expect(activeExtensionRow.getByRole('button', { name: 'Продлить доступ', exact: true })).toHaveCount(0);
    await expect(activeExtensionRow.getByRole('button', { name: 'Копировать', exact: true })).toHaveCount(0);
    await expect(activeExtensionRow.getByRole('button', { name: 'Редактировать', exact: true })).toHaveCount(1);
    await expect(initialSurveyRows.filter({ hasText: localDisplayDaysAgo(-30) })).toBeVisible();

    await initialSurveyRows.nth(0).getByRole('button', { name: 'Продлить доступ', exact: true }).click();
    const extensionCreateModal = page.locator('#surveyExtensionModal');
    await expect(extensionCreateModal).toBeVisible();
    await extensionCreateModal.locator('[data-role="organization-trigger"]').click();
    await expect(extensionCreateModal.locator('[data-role="organization-options"]')).toContainText('Smoke org');
    await expect(extensionCreateModal.locator('[data-role="organization-options"]')).not.toContainText('Smoke unrelated org');
    await extensionCreateModal.getByRole('button', { name: 'Отмена', exact: true }).click();
    await expect(extensionCreateModal).toBeHidden();

    await activeExtensionRow.locator('.survey-table__name-cell').click();
    const extensionDetailsModal = page.locator('#surveyDetailsModal');
    await expect(extensionDetailsModal).toBeVisible();
    await expect(extensionDetailsModal.getByRole('heading', { name: 'Просмотр продления', exact: true })).toBeVisible();
    await expect(extensionDetailsModal).toContainText('Smoke survey');
    await expect(extensionDetailsModal).not.toContainText('Smoke survey: продление для Smoke org');
    await expect(extensionDetailsModal).toContainText(localDisplayDaysAgo(1));
    await expect(extensionDetailsModal).toContainText(localDisplayDaysAgo(-30));
    await expect(extensionDetailsModal).toContainText('Smoke org');
    await expect(extensionDetailsModal.getByText('Организация', { exact: true })).toBeVisible();
    await expect(extensionDetailsModal.getByText('Организации', { exact: true })).toHaveCount(0);
    await expect(extensionDetailsModal.locator('.survey-details-modal__organizations')).toHaveText('Smoke org');
    await expect(extensionDetailsModal.locator('.survey-details-modal__organizations .app-chip')).toHaveCount(0);
    await extensionDetailsModal.getByRole('button', { name: 'Закрыть', exact: true }).click();
    await expect(extensionDetailsModal).toBeHidden();

    await activeExtensionRow.getByRole('button', { name: 'Редактировать', exact: true }).click();
    const extensionPeriodModal = page.locator('#surveyExtensionPeriodModal');
    await expect(extensionPeriodModal).toBeVisible();
    await expect(extensionPeriodModal).toContainText('Редактирование продления');
    await expect(extensionPeriodModal).toContainText('Smoke survey');
    await expect(extensionPeriodModal).not.toContainText('Smoke survey: продление для Smoke org');
    await expect(extensionPeriodModal).toContainText('Smoke org');
    await expect(extensionPeriodModal.locator('#extensionPeriodDateBegin')).toHaveCount(0);
    await extensionPeriodModal.locator('#extensionPeriodDateEnd').fill(localIsoDaysAgo(-35));
    const extensionUpdateResponsePromise = page.waitForResponse((response) => (
        response.request().method() === 'POST'
        && /\/survey\/\d+\/extensions\/\d+\/period$/.test(new URL(response.url()).pathname)
    ));
    await extensionPeriodModal.getByRole('button', { name: 'Сохранить', exact: true }).click();
    const extensionUpdateResponse = await extensionUpdateResponsePromise;
    expect(extensionUpdateResponse.status()).toBe(200);
    await expect(page.locator('.site-toast--success').filter({ hasText: 'Дата конца продления успешно изменена.' }).last()).toBeVisible();
    await expect(page.locator('[data-role="admin-survey-row"]')
        .filter({ hasText: 'Smoke survey: продление для Smoke org' })
        .filter({ hasText: localDisplayDaysAgo(-35) }))
        .toBeVisible();

    await page.goto('/survey/archive');
    const initialArchivedSurveyRows = page.locator('[data-role="admin-survey-row"]');
    await expect(initialArchivedSurveyRows).toHaveCount(2);
    await expect(initialArchivedSurveyRows.nth(0)).toHaveAttribute('data-is-extension', 'false');
    await expect(initialArchivedSurveyRows.nth(1)).toHaveAttribute('data-is-extension', 'true');
    const archivedExtensionRow = initialArchivedSurveyRows.filter({
        hasText: 'Smoke archived extension survey: продление для Smoke archived org'
    });
    await expect(archivedExtensionRow).toBeVisible();
    await expect(archivedExtensionRow.locator('[data-role="survey-extension-name"]'))
        .toHaveText(/^\s*↳\s*Smoke archived extension survey: продление для Smoke archived org\s*$/);
    await expect(archivedExtensionRow.getByRole('link', { name: 'Проверить прохождение', exact: true })).toHaveCount(0);
    await expect(archivedExtensionRow.getByRole('button', { name: 'Продлить доступ', exact: true })).toHaveCount(0);
    await expect(archivedExtensionRow.getByRole('button', { name: 'Копировать', exact: true })).toHaveCount(0);
    await expect(initialArchivedSurveyRows.filter({ hasText: localDisplayDaysAgo(30) })).toBeVisible();

    await archivedExtensionRow.locator('.survey-table__name-cell').click();
    await expect(extensionDetailsModal).toBeVisible();
    await expect(extensionDetailsModal.getByRole('heading', { name: 'Просмотр продления', exact: true })).toBeVisible();
    await expect(extensionDetailsModal).toContainText('Smoke archived extension survey');
    await expect(extensionDetailsModal)
        .not.toContainText('Smoke archived extension survey: продление для Smoke archived org');
    await expect(extensionDetailsModal).toContainText(localDisplayDaysAgo(60));
    await expect(extensionDetailsModal).toContainText(localDisplayDaysAgo(30));
    await expect(extensionDetailsModal).toContainText('Smoke archived org');
    await extensionDetailsModal.getByRole('button', { name: 'Закрыть', exact: true }).click();
    await expect(extensionDetailsModal).toBeHidden();

    await page.goto('/surveys');
    await expect(page.locator('[data-page="surveys-list"]')).toBeVisible();
    await page.locator('[data-role="survey-organization-filter-trigger"]').click();
    await expect(page.locator('[data-role="survey-organization-filter-popover"]')).not.toHaveClass(/is-hidden/);
    await expect(page.locator('[data-role="survey-organization-filter"]')).toHaveCSS('z-index', '10');
    await page.locator('[data-role="survey-organization-filter-option"]').first().check();
    await page.locator('[data-role="survey-organization-filter-close"]').click();
    await expect(page).toHaveURL(/organizationIds=\d+/);
    await expect(page.locator('[data-role="survey-organization-filter-label"]')).toHaveText('Организации: 1');

    const organizationInlineClear = page.locator('[data-role="survey-organization-filter-inline-clear"]');
    await expect(organizationInlineClear).toBeVisible();
    await organizationInlineClear.click();
    await expect(page).not.toHaveURL(/organizationIds=/);

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
    await page.getByRole('button', { name: 'Применить', exact: true }).click();
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
                message: 'Нельзя удалить анкету "Smoke survey": по ней есть ответы.'
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
                message: 'Нельзя удалить пользователя "Smoke client": он связан с сохранёнными ответами анкет.'
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

    await page.unroute(/\/users\/\d+\/delete$/);
    await page.goto('/users?page=2&sortBy=name&sortDirection=asc');
    const paginatedUsersUrl = page.url();
    const paginatedUserRows = page.locator('[data-role="user-row"]');
    await expect(paginatedUserRows).toHaveCount(10);
    const deletedLogin = await paginatedUserRows.first().getAttribute('data-user-name');
    await paginatedUserRows.first().locator('[data-click-call="deleteUserFromTrigger"]').click();
    await page.locator('.site-confirm__button--confirm').click();
    await expect(page.locator('.site-toast--success')
        .filter({ hasText: 'Пользователь успешно удалён.' })
        .last()).toBeVisible();
    await expect(page).toHaveURL(paginatedUsersUrl);
    await expect(page.locator('[data-role="user-row"]')).toHaveCount(10);
    await expect(page.locator(`[data-role="user-row"][data-user-name="${deletedLogin}"]`)).toHaveCount(0);
    await expect(page.locator('.site-toast--error')
        .filter({ hasText: 'closeModal is not defined' }))
        .toHaveCount(0);

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

    await expect(page.locator('[data-app-shell="client"]')).toHaveCount(1);
    await expect(page.locator('#chrome-header .app-header--client')).toBeVisible();
    await expect(page.locator('#chrome-navigation')).toHaveCount(0);
    await expect(page.locator('#content_admin')).toHaveCSS('background-color', 'rgb(255, 255, 255)');

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

    await page.locator('[data-role="survey-name-filter-trigger"]').click();
    await page.locator('[data-role="survey-name-filter-option"]').first().check();
    await page.locator('[data-role="survey-name-filter-close"]').click();
    await expect(page).toHaveURL(/surveyIds=\d+/);
    await expect(page.locator('[data-role="survey-name-filter-label"]')).toHaveText('Анкеты: 1');
    const surveyInlineClear = page.locator('[data-role="survey-name-filter-inline-clear"]');
    await expect(surveyInlineClear).toBeVisible();

    const currentMonthIndex = new Date().getMonth();
    const currentMonth = `${new Date().getFullYear()}-${String(currentMonthIndex + 1).padStart(2, '0')}`;
    await page.locator('[data-role="survey-date-filter-trigger"]').click();
    await page.locator(`[data-role="survey-date-filter-month"][data-month-index="${currentMonthIndex}"]`).click();
    await page.locator('[data-role="survey-date-filter-close"]').click();
    await expect(page).toHaveURL(new RegExp(`month=${currentMonth}`));
    await expect(archivedSurveyRow).toHaveCount(1);

    await page.goBack();
    await expect(page).not.toHaveURL(/month=/);
    await expect(page).toHaveURL(/surveyIds=\d+/);
    await expect(page.locator('[data-role="survey-date-filter-label"]')).toHaveText('Фильтр по периоду');

    await page.goForward();
    await expect(page).toHaveURL(new RegExp(`month=${currentMonth}`));
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
    await expect(page.locator('#surveyAnswersTitle')).toHaveText('Просмотр ответов');
    await expect(page.locator('#answersContainer .answers-modal__table tbody tr')).toHaveCount(1);
    await expect(page.locator('#answersContainer .answers-modal__table-wrap')).toHaveCSS('padding-bottom', '0px');
    await expect(page.locator('#answersContainer .answers-modal__table')).toHaveCSS('margin-bottom', '0px');

    await page.locator('#answersModal [data-modal-close="answersModal"]').click();
    const deleteAnswerButton = answerJournalRow.getByRole('button', { name: 'Удалить', exact: true });
    await expect(deleteAnswerButton).toBeVisible();
    const answerDeleted = page.waitForResponse((response) => (
        response.request().method() === 'POST'
        && /\/answers\/\d+\/delete$/.test(new URL(response.url()).pathname)
        && response.status() === 200
    ));
    await deleteAnswerButton.click();
    await expect(page.locator('.site-confirm__title')).toHaveText('Удаление ответа');
    await page.locator('.site-confirm__button--confirm').click();
    await answerDeleted;
    await expect(page.locator('.site-toast--success')
        .filter({ hasText: 'Ответ успешно удалён.' })
        .last()).toBeVisible();
    await expect(page.locator('.answers-page__row')).toHaveCount(0);
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
