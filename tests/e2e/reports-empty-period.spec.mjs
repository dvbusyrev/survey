import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

test('отчёт без ответов объясняет причину отказа', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body></body></html>'
    }));
    await page.route('http://survey.test/reports/quarterly/4/2026', (route) => route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({
            success: false,
            error: 'За выбранный квартал и год нет ответов для формирования отчёта.',
            message: 'За выбранный квартал и год нет ответов для формирования отчёта.'
        })
    }));

    await page.goto('http://survey.test/reports');
    await page.evaluate(() => {
        window.__reportNotification = null;
        window.AppUi = {
            notify(message, type, options) {
                window.__reportNotification = { message, type, title: options?.title };
            }
        };
    });
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/features/admin/admin-reports.js')
    });

    const result = await page.evaluate(() => window.createQuarterlyReport(4, 2026));

    expect(result).toBe(false);
    expect(await page.evaluate(() => window.__reportNotification)).toEqual({
        message: 'Причина: За выбранный квартал и год нет ответов для формирования отчёта.',
        type: 'error',
        title: 'Квартальный отчёт не сформирован'
    });
});
