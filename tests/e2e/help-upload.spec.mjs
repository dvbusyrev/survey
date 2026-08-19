import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

test('загрузка инструкции отправляет antiforgery-токен', async ({ page }) => {
    let actualToken = '';
    await page.route('http://survey.test/**', async (route) => {
        const request = route.request();
        if (new URL(request.url()).pathname === '/help/upload') {
            actualToken = request.headers()['requestverificationtoken'] || '';
            await route.fulfill({
                status: 200,
                contentType: 'application/json',
                body: JSON.stringify({
                    message: 'Файл успешно загружен.',
                    displayText: 'Новая инструкция.docx 19.08.2026'
                })
            });
            return;
        }

        await route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><html></html>' });
    });

    await page.goto('http://survey.test/help');
    await page.setContent(`
        <div id="global-antiforgery-token" hidden>
            <input name="__RequestVerificationToken" value="test-antiforgery-token">
        </div>
        <div data-page="help-page">
            <input data-help-display="admin-guide" readonly>
            <input type="file"
                   data-help-file-input
                   data-help-type="admin-guide"
                   data-help-role="admin">
        </div>
    `);
    await page.evaluate(() => {
        window.__helpNotification = null;
        window.AppUi = {
            notify(message, type) {
                window.__helpNotification = { message, type };
            }
        };
    });
    await page.addScriptTag({ path: path.join(projectRoot, 'wwwroot/js/core/app-http.js') });
    await page.addScriptTag({ path: path.join(projectRoot, 'wwwroot/js/features/help/help-page.js') });

    await page.locator('[data-help-file-input]').setInputFiles({
        name: 'Новая инструкция.docx',
        mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        buffer: Buffer.from('test document')
    });

    await expect.poll(() => actualToken).toBe('test-antiforgery-token');
    await expect.poll(() => page.evaluate(() => window.__helpNotification)).toEqual({
        message: 'Файл успешно загружен.',
        type: 'success'
    });
});
