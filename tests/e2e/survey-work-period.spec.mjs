import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function toIso(date) {
    const pad = (value) => String(value).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function shiftDay(source, offset) {
    return new Date(source.getFullYear(), source.getMonth(), source.getDate() + offset);
}

test('период работы блокирует неверные даты и позволяет сбросить диапазон', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body></body></html>'
    }));
    await page.goto('http://survey.test/survey');
    await page.setContent(`
        <main class="app-page" data-page="surveys-list">
            <div class="survey-period-filter survey-work-period" data-role="survey-work-period">
                <button type="button" data-role="survey-work-period-trigger"></button>
                <div class="survey-period-filter__popover survey-work-period__popover"
                     data-role="survey-work-period-popover">
                    <button type="button" data-role="survey-work-period-prev"></button>
                    <span data-role="survey-work-period-label"></span>
                    <button type="button" data-role="survey-work-period-next"></button>
                    <button type="button" data-role="survey-work-period-close"></button>
                    <div data-role="survey-work-period-calendar"></div>
                    <button type="button"
                            class="app-button app-button--secondary survey-period-filter__clear-button survey-work-period__reset-button"
                            data-role="survey-work-period-reset"
                            disabled>Сбросить</button>
                    <button type="button"
                            class="app-button app-button--primary survey-period-filter__save-button"
                            data-role="survey-work-period-save"
                            disabled>Сохранить</button>
                </div>
            </div>
        </main>
    `);
    await page.addStyleTag({
        content: ':root { --text-main: #111; --text-secondary: #68707a; --border: #cfd3d8; --app-theme-button-color: #656169; --app-theme-button-text-color: #fff; }'
    });
    await page.addStyleTag({ path: path.join(projectRoot, 'wwwroot/css/pages/survey-admin-pages.css') });
    await page.evaluate(() => {
        window.AppUi = {
            notify() {}
        };
    });
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/features/survey/survey-work-period.js')
    });
    await page.evaluate(() => window.SurveyWorkPeriod.mount(document));

    const today = new Date();
    const yesterdayIso = toIso(shiftDay(today, -1));
    const todayIso = toIso(today);
    const tomorrowIso = toIso(shiftDay(today, 1));
    const twoDaysAheadIso = toIso(shiftDay(today, 2));
    const resetButton = page.locator('[data-role="survey-work-period-reset"]');
    const saveButton = page.locator('[data-role="survey-work-period-save"]');

    await expect(page.locator(`[data-date-iso="${tomorrowIso}"]`)).toBeDisabled();
    await expect(page.locator(`[data-date-iso="${tomorrowIso}"]`)).toHaveCSS('cursor', 'not-allowed');
    await expect(page.locator(`[data-date-iso="${todayIso}"]`)).toBeEnabled();
    await expect(resetButton).toBeDisabled();

    await page.locator(`[data-date-iso="${yesterdayIso}"]`).click();
    await expect(page.locator(`[data-date-iso="${yesterdayIso}"]`)).toHaveClass(/is-range-single/);
    await expect(page.locator(`[data-date-iso="${yesterdayIso}"]`)).toBeDisabled();
    await expect(page.locator(`[data-date-iso="${todayIso}"]`)).toBeEnabled();
    await expect(resetButton).toBeEnabled();

    await page.locator(`[data-date-iso="${twoDaysAheadIso}"]`).click();
    await expect(saveButton).toBeEnabled();
    await expect(resetButton).toHaveCSS('background-color', 'rgb(255, 255, 255)');

    await resetButton.click();
    await expect(page.locator('.is-range-start, .is-range-end, .is-range-single, .is-in-range')).toHaveCount(0);
    await expect(resetButton).toBeDisabled();
    await expect(saveButton).toBeDisabled();
});
