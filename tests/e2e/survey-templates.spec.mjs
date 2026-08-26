import { expect, test } from '@playwright/test';

async function loginAsAdmin(page) {
    await page.goto('/');
    await page.locator('#username').fill('smoke-admin');
    await page.locator('#password').fill('SmokePass1!');
    await page.getByRole('button', { name: 'Войти', exact: true }).click();
    await expect(page).toHaveURL(/\/survey$/);
}

function localIsoDaysFromToday(days) {
    const date = new Date();
    date.setHours(12, 0, 0, 0);
    date.setDate(date.getDate() + days);
    return [
        date.getFullYear(),
        String(date.getMonth() + 1).padStart(2, '0'),
        String(date.getDate()).padStart(2, '0')
    ].join('-');
}

test('шаблоны отделены от анкет и открывают собственную форму', async ({ page }) => {
    await loginAsAdmin(page);

    const navigationItems = page.locator('#chrome-navigation .nav-list > .nav-item');
    await expect(navigationItems.nth(0)).toContainText('Статистика');
    await expect(navigationItems.nth(1)).toContainText('Шаблоны анкет');

    await page.goto('/survey-templates');
    await expect(page.getByRole('tab', { name: 'Активные шаблоны', exact: true })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Плановые шаблоны', exact: true })).toBeVisible();
    await expect(page.getByRole('tab', { name: 'Архивные шаблоны', exact: true })).toBeVisible();
    await expect(page.locator('[data-role="admin-survey-row"]')).toHaveCount(1);
    await expect(page.locator('[data-role="admin-survey-row"]')).toContainText('Smoke active template');
    await expect(page.locator('[data-role="admin-survey-row"]')).not.toContainText('Smoke survey');
    await expect(page.locator('thead a.logs-table__sort-link', { hasText: 'Дата начала' })).toBeVisible();
    await expect(page.locator('thead a.logs-table__sort-link', { hasText: 'Дата конца' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Проверить прохождение', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Продлить доступ', exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Период работы', exact: true })).toHaveCount(0);
    await expect(page.locator('thead')).toContainText('Добавлено в автосоздание');
    const autoCreationSortLink = page.locator('thead a.logs-table__sort-link', {
        hasText: 'Добавлено в автосоздание'
    });
    await expect(autoCreationSortLink).toHaveAttribute('href', /sortBy=autoCreation/);
    await expect(page.locator('[data-role="admin-survey-row"]')).toContainText('Да');
    const toolbarItems = page.locator('.survey-list-toolbar > *');
    await expect(toolbarItems.nth(0)).toHaveText('Добавить шаблон');
    await expect(toolbarItems.nth(1)).toHaveAttribute('data-role', 'survey-organization-filter');

    const editLink = page.getByRole('link', { name: 'Редактировать', exact: true });
    await expect(editLink).toHaveAttribute('href', /\/survey-templates\/\d+\/edit$/);

    await page.getByRole('button', { name: 'Добавить шаблон', exact: true }).click();
    const editor = page.locator('#surveyEditorModal');
    await expect(editor).toBeVisible();
    await expect(editor.getByRole('heading', { name: 'Добавление шаблона', exact: true })).toBeVisible();
    await expect(editor.getByText('Добавлено в автосоздание', { exact: true })).toBeVisible();
    await expect(editor.locator('#surveyAutoCreationEnabled')).toHaveValue('false');
    const autoCreationTrigger = editor.locator('[data-role="survey-auto-creation-enabled-trigger"]');
    const autoCreationMenu = editor.locator('[data-role="survey-auto-creation-enabled-menu"]');
    await expect(autoCreationTrigger).toContainText('Нет');
    await autoCreationTrigger.click();
    await expect(autoCreationMenu).toBeVisible();
    const autoCreationTriggerBox = await autoCreationTrigger.boundingBox();
    const autoCreationMenuBox = await autoCreationMenu.boundingBox();
    expect(autoCreationTriggerBox).not.toBeNull();
    expect(autoCreationMenuBox).not.toBeNull();
    expect(autoCreationMenuBox.y).toBeGreaterThanOrEqual(
        autoCreationTriggerBox.y + autoCreationTriggerBox.height
    );
    await autoCreationMenu.getByRole('option', { name: 'Да', exact: true }).click();
    await expect(editor.locator('#surveyAutoCreationEnabled')).toHaveValue('true');
    await expect(autoCreationTrigger).toContainText('Да');
    await expect(editor.getByText('Дата конца', { exact: true })).toBeVisible();
    const requiredLabels = [
        [editor.locator('label[for="surveyTitle"]'), 'Название шаблона'],
        [editor.locator('label[for="startDate"]'), 'Дата начала'],
        [editor.locator('.survey-editor-page__organization-field > label'), 'Организации'],
        [editor.locator('label[for="criterion1"]'), 'Критерий №1']
    ];
    for (const [label, text] of requiredLabels) {
        await expect(label).toHaveText(text);
        expect(await label.evaluate((element) => getComputedStyle(element, '::after').content)).toBe('" *"');
    }
    expect(await editor.locator('#endDate').getAttribute('required')).toBeNull();
    await expect(editor.getByPlaceholder('Введите название шаблона', { exact: true })).toBeVisible();
    await editor.getByRole('button', { name: 'Отмена', exact: true }).click();

    await editLink.click();
    const editEditor = page.locator('#surveyEditorModal');
    await expect(editEditor.getByRole('heading', { name: 'Редактирование шаблона', exact: true })).toBeVisible();
    await expect(editEditor.locator('#surveyAutoCreationEnabled')).toHaveValue('true');
    const editAutoCreationTrigger = editEditor.locator('[data-role="survey-auto-creation-enabled-trigger"]');
    await expect(editAutoCreationTrigger).toContainText('Да');
    await editAutoCreationTrigger.click();
    const editAutoCreationMenu = editEditor.locator('[data-role="survey-auto-creation-enabled-menu"]');
    await expect(editAutoCreationMenu.getByRole('option')).toHaveCount(2);
    await editAutoCreationMenu.getByRole('option', { name: 'Нет', exact: true }).click();
    await expect(editEditor.locator('#surveyAutoCreationEnabled')).toHaveValue('false');
    await editEditor.getByRole('button', { name: 'Отмена', exact: true }).click();

    await page.goto('/survey-templates/planned');
    await expect(page.getByRole('tab', { name: 'Плановые шаблоны', exact: true })).toHaveAttribute('aria-selected', 'true');
    const plannedRow = page.locator('[data-role="admin-survey-row"]');
    await expect(plannedRow).toHaveCount(1);
    await expect(plannedRow).toContainText('Smoke planned template');
    await expect(plannedRow).toContainText('Да');
    await expect(page.getByText('Smoke active template', { exact: true })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Создать плановый шаблон', exact: true })).toBeVisible();
    const plannedEditLink = page.getByRole('link', { name: 'Редактировать', exact: true });
    await expect(plannedEditLink).toHaveAttribute('href', /\/survey-templates\/planned\/\d+\/edit$/);

    await page.getByRole('button', { name: 'Создать плановый шаблон', exact: true }).click();
    const plannedEditor = page.locator('#surveyEditorModal');
    await expect(plannedEditor.getByRole('heading', { name: 'Создание планового шаблона', exact: true })).toBeVisible();
    const parentField = plannedEditor.locator('[data-role="survey-template-field"]');
    const titleGroup = plannedEditor.locator('#surveyTitle').locator('..');
    expect(await parentField.evaluate((parent, title) => (
        Boolean(parent.compareDocumentPosition(title) & Node.DOCUMENT_POSITION_FOLLOWING)
    ), await titleGroup.elementHandle())).toBe(true);
    const parentTrigger = plannedEditor.locator('[data-role="survey-template-dropdown-trigger"]');
    const parentMenu = plannedEditor.locator('[data-role="survey-template-dropdown-menu"]');
    await expect(parentTrigger).toContainText('Не выбран');
    await parentTrigger.click();
    await expect(parentMenu).toBeVisible();
    const parentTriggerBox = await parentTrigger.boundingBox();
    const parentMenuBox = await parentMenu.boundingBox();
    expect(parentMenuBox.y).toBeGreaterThanOrEqual(parentTriggerBox.y + parentTriggerBox.height);
    await expect(parentMenu.getByRole('option', { name: 'Smoke active template', exact: true })).toBeVisible();
    await expect(parentMenu.getByRole('option', { name: 'Smoke planned template', exact: true })).toHaveCount(0);
    await parentMenu.getByRole('option', { name: 'Smoke active template', exact: true }).click();
    await expect(plannedEditor.locator('#plannedTemplateAncestorId')).not.toHaveValue('');
    await expect(parentTrigger).toContainText('Smoke active template');
    await expect(plannedEditor.locator('#surveyTitle')).toHaveValue('Smoke active template');
    await expect(plannedEditor.locator('#surveyDescription'))
        .toHaveValue('Active template used only by browser smoke tests');
    await expect(plannedEditor.locator('.criteriy')).toHaveValue('Smoke template question');
    await expect(plannedEditor.locator('#startDate')).toHaveValue('');
    await expect(plannedEditor.locator('#endDate')).toHaveValue('');
    await expect(plannedEditor.locator('#startDate')).toHaveAttribute('min', localIsoDaysFromToday(1));
    await plannedEditor.getByRole('button', { name: 'Отмена', exact: true }).click();

    await page.goto('/survey-templates/archive');
    await expect(page.locator('[data-role="admin-survey-row"]')).toHaveCount(1);
    await expect(page.locator('[data-role="admin-survey-row"]')).toContainText('Smoke archived template');
    await expect(page.getByRole('link', { name: 'Редактировать', exact: true })).toHaveCount(0);
    await expect(page.locator('[data-role="survey-name-filter"]')).toHaveCount(0);
    await expect(page.locator('thead')).not.toContainText('Добавлено в автосоздание');

    await page.goto('/settings/survey-creation');
    const autoCreationPage = page.locator('[data-page="survey-auto-creation"]');
    await expect(autoCreationPage.getByText('Шаблоны анкет', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Выбрать шаблоны', exact: true })).toBeVisible();
    await expect(page.locator('[data-role="survey-auto-creation-selected-list"]'))
        .toContainText('Smoke active template');
    await expect(page.locator('[data-role="survey-auto-creation-selected-list"]'))
        .toContainText('Smoke planned template');
    await expect(page.getByRole('button', { name: 'Применить', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Сохранить', exact: true })).toHaveCount(0);
    const clearSelectedTemplates = page.getByRole('button', {
        name: 'Очистить выбранные шаблоны',
        exact: true
    });
    await expect(clearSelectedTemplates).toBeVisible();
    await clearSelectedTemplates.click();
    await expect(page.locator('[data-role="survey-auto-creation-selected-list"]'))
        .toContainText('Шаблоны не выбраны');
    await page.reload();
    await expect(page.locator('[data-role="survey-auto-creation-selected-list"]'))
        .toContainText('Smoke active template');
    await page.getByRole('button', { name: 'Выбрать шаблоны', exact: true }).click();
    const autoCreationOptions = page.locator('#surveyAutoCreationModalList');
    await expect(autoCreationOptions).toContainText('Smoke active template');
    await expect(autoCreationOptions).not.toContainText('Smoke planned template');
    await expect(autoCreationOptions).not.toContainText('Smoke archived template');

    await page.goto('/survey');
    await expect(page.getByText('Smoke active template', { exact: true })).toHaveCount(0);

    await page.getByRole('button', { name: 'Добавить анкету', exact: true }).click();
    const surveyEditor = page.locator('#surveyEditorModal');
    await expect(surveyEditor.getByText('Шаблон анкеты', { exact: true })).toHaveCount(0);
    await expect(surveyEditor.locator('[data-role="survey-template-dropdown-trigger"]')).toHaveText('Выбрать шаблон');
    const titleFieldGroup = surveyEditor.locator('#surveyTitle').locator('..');
    const templateFieldGroup = surveyEditor.locator('[data-role="survey-template-field"]');
    expect(await titleFieldGroup.evaluate((titleGroup, templateGroup) => (
        titleGroup.contains(templateGroup)
    ), await templateFieldGroup.elementHandle())).toBe(true);
    await surveyEditor.locator('#startDate').fill(localIsoDaysFromToday(0));
    await surveyEditor.locator('#endDate').fill(localIsoDaysFromToday(1));
    const startDateBeforeTemplate = await surveyEditor.locator('#startDate').inputValue();
    const endDateBeforeTemplate = await surveyEditor.locator('#endDate').inputValue();
    const templateTrigger = surveyEditor.locator('[data-role="survey-template-dropdown-trigger"]');
    const templateMenu = surveyEditor.locator('[data-role="survey-template-dropdown-menu"]');
    await templateTrigger.click();
    const templateOptions = surveyEditor.locator('[data-role="survey-template-options"]');
    await expect(templateOptions.getByRole('option')).toHaveCount(1);
    await expect(templateOptions.getByRole('option', { name: 'Smoke archived template', exact: true })).toHaveCount(0);
    await expect(templateOptions.locator('input[type="checkbox"]')).toHaveCount(0);
    const triggerBox = await templateTrigger.boundingBox();
    const menuBox = await templateMenu.boundingBox();
    const descriptionLabelBox = await surveyEditor.locator('label[for="surveyDescription"]').boundingBox();
    expect(triggerBox).not.toBeNull();
    expect(menuBox).not.toBeNull();
    expect(descriptionLabelBox).not.toBeNull();
    expect(menuBox.y).toBeGreaterThanOrEqual(triggerBox.y + triggerBox.height);
    expect(descriptionLabelBox.y).toBeGreaterThanOrEqual(menuBox.y + menuBox.height);
    await templateOptions.getByRole('option', { name: 'Smoke active template', exact: true }).click();
    await expect(surveyEditor.locator('#surveyTitle')).toHaveValue('Smoke active template');
    await expect(surveyEditor.locator('#surveyDescription'))
        .toHaveValue('Active template used only by browser smoke tests');
    await expect(surveyEditor.locator('.criteriy')).toHaveValue('Smoke template question');
    await expect(surveyEditor.locator('[data-role="selected-organizations-list"]')).toContainText('Smoke org');
    await expect(surveyEditor.locator('#startDate')).toHaveValue(startDateBeforeTemplate);
    await expect(surveyEditor.locator('#endDate')).toHaveValue(endDateBeforeTemplate);
    await surveyEditor.getByRole('button', { name: 'Отмена', exact: true }).click();

    await page.goto('/survey/archive');
    await expect(page.getByText('Smoke archived template', { exact: true })).toHaveCount(0);
    await expect(page.locator('[data-role="survey-name-filter"]')).toHaveCount(1);

    await page.goto('/survey-templates?organizationIds=999999');
    const emptyTemplatesTable = page.locator('[data-role="main-table"]');
    await expect(emptyTemplatesTable).toBeVisible();
    await expect(emptyTemplatesTable.locator('[data-role="admin-survey-row"]')).toHaveCount(0);
    await expect(emptyTemplatesTable.locator('tbody')).not.toContainText(/не найдены/i);
});
