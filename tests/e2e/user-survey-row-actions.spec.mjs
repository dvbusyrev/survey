import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const scriptsRoot = path.join(projectRoot, 'wwwroot/js');

test('незаполненная архивная анкета не открывает просмотр ответов', async ({ page }) => {
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html',
        body: '<!doctype html><html></html>'
    }));
    await page.route('http://survey.test/js/**', async (route) => {
        const relativePath = decodeURIComponent(new URL(route.request().url()).pathname)
            .replace(/^\/js\//, '');
        const scriptPath = path.resolve(scriptsRoot, relativePath);
        if (!scriptPath.startsWith(`${scriptsRoot}${path.sep}`)) {
            await route.abort();
            return;
        }

        await route.fulfill({
            status: 200,
            contentType: 'text/javascript',
            body: await fs.readFile(scriptPath, 'utf8')
        });
    });

    await page.goto('http://survey.test/archive');
    await page.setContent(`
        <div id="survey-list">
            <table>
                <tbody>
                    <tr data-role="user-survey-row" data-row-action="" data-survey-id="1">
                        <td>Не заполнена</td>
                    </tr>
                    <tr data-role="user-survey-row" data-row-action="view" data-survey-id="2">
                        <td>Заполнена</td>
                    </tr>
                </tbody>
            </table>
        </div>
    `);

    await page.evaluate(async () => {
        const { createSurveyUserListInteractionController } = await import(
            '/js/features/survey/user-survey-list.js'
        );
        window.__openedSurveyIds = [];
        window.__rowController = createSurveyUserListInteractionController({
            contentHost: document.getElementById('survey-list'),
            state: {},
            rowTooltip: { hide() {} },
            openSurveyById(id) {
                window.__openedSurveyIds.push(id);
            }
        });
    });

    await page.getByText('Не заполнена', { exact: true }).click();
    expect(await page.evaluate(() => window.__openedSurveyIds)).toEqual([]);

    await page.getByText('Заполнена', { exact: true }).click();
    expect(await page.evaluate(() => window.__openedSurveyIds)).toEqual([2]);
});
