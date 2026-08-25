import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

const modalMarkup = `
    <template id="admin-extension-modal-template">
        <div class="admin-extension-modal-root">
            <h2>Продление анкеты</h2>
            <div data-role="survey-name"></div>
            <div data-role="error" class="is-hidden"></div>
            <div data-role="rows-container"></div>
            <p data-role="empty-state" class="is-hidden">Доступных организаций для продления не найдено.</p>
            <button type="button" data-role="cancel">Отмена</button>
            <button type="button" data-role="submit">Продлить доступ</button>
        </div>
    </template>
    <template id="admin-extension-modal-row-template">
        <div class="admin-extension-row">
            <div>
                <label>Организации</label>
                <div class="admin-extension-selected-organizations">
                    <div data-role="organization-selection"></div>
                </div>
                <div data-role="organization-dropdown">
                    <button type="button" data-role="organization-trigger">Выбрать организации</button>
                    <div data-role="organization-panel" class="is-hidden">
                        <div data-role="organization-options"></div>
                    </div>
                </div>
            </div>
            <div>
                <label>Доступно по</label>
                <input type="date" data-role="date-input" />
            </div>
        </div>
    </template>
    <div id="modal-host"></div>
`;

async function mountModal(page) {
    await page.setContent(modalMarkup);
    await page.evaluate(() => {
        window.AppUi = {
            createElement(tagName, options = {}) {
                const element = document.createElement(tagName);
                element.className = options.className || '';
                element.textContent = options.text || '';
                return element;
            },
            createCheckboxOption(options = {}) {
                const option = document.createElement('label');
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = Boolean(options.checked);
                option.append(checkbox, document.createTextNode(options.text || ''));
                return { option, checkbox };
            },
            createMultiselect() {
                return {
                    controller: { open() {}, close() {} },
                    destroy() {}
                };
            },
            notify(message, type) {
                window.__extensionNotification = { message, type };
            }
        };
        window.AppDate = {
            todayIso: () => '2026-08-24',
            parseDate: (value) => value ? new Date(`${value}T00:00:00`) : null,
            toIso: (value) => value.toISOString().slice(0, 10),
            compare: (left, right) => String(left).localeCompare(String(right)),
            enhanceDateInputs() {},
            setInputValue(input, value) { input.value = value; },
            getInputIso: (input) => input.value
        };
        window.AppCheckboxDropdown = { scheduleListHeightUpdate() {} };
    });
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/features/survey/survey-extension-modal.js')
    });
    await page.evaluate(() => {
        window.AdminSurveyExtensionModal.mount(document.getElementById('modal-host'), {
            survey: {
                id_survey: 336,
                name_survey: 'Архивная анкета',
                date_end: '2026-07-31'
            }
        });
    });
}

test('модалка продления архивной анкеты показывает организации и дату конца', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body></body></html>'
    }));
    await page.route('http://survey.test/survey/336/assigned-organizations', (route) => route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
            {
                id: 17,
                name: 'Тестовая организация',
                dateEnd: '2026-07-31',
                surveyDateEnd: '2026-07-31'
            }
        ])
    }));
    await page.goto('http://survey.test/survey/archive');
    await mountModal(page);

    await expect(page.getByText('Организации', { exact: true })).toBeVisible();
    await expect(page.getByText('Доступно по', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Выбрать организации' })).toBeEnabled();
    await expect(page.getByText('Тестовая организация')).toHaveCount(1);
    await expect(page.locator('[data-role="date-input"]')).toBeEnabled();
    await expect(page.locator('[data-role="date-input"]')).toHaveAttribute('min', '2026-08-24');
});

test('модалка продления показывает ошибку загрузки и сохраняет поля', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body></body></html>'
    }));
    await page.route('http://survey.test/survey/336/assigned-organizations', (route) => route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Не удалось загрузить назначенные организации.' })
    }));
    await page.goto('http://survey.test/survey/archive');
    await mountModal(page);

    await expect(page.getByText('Организации', { exact: true })).toBeVisible();
    await expect(page.getByText('Доступно по', { exact: true })).toBeVisible();
    await expect(page.locator('[data-role="error"]')).toContainText('Не удалось загрузить назначенные организации');
    await expect(page.locator('[data-role="error"]')).toBeVisible();
    await expect(page.locator('[data-role="organization-trigger"]')).toBeDisabled();
    await expect(page.locator('[data-role="date-input"]')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Продлить доступ' })).toBeDisabled();
});

test('модалка объясняет отсутствие организаций, не скрывая поля', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body></body></html>'
    }));
    await page.route('http://survey.test/survey/336/assigned-organizations', (route) => route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: '[]'
    }));
    await page.goto('http://survey.test/survey/archive');
    await mountModal(page);

    await expect(page.getByText('Доступных организаций для продления не найдено.')).toBeVisible();
    await expect(page.getByText('Организации', { exact: true })).toBeVisible();
    await expect(page.getByText('Доступно по', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Организации недоступны' })).toBeDisabled();
    await expect(page.locator('[data-role="date-input"]')).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Продлить доступ' })).toBeDisabled();
});
