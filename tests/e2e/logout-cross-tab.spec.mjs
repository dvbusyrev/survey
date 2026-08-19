import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

test('запрос после выхода в другой вкладке возвращает пользователя на вход', async ({ page }) => {
    let ajaxHeader = '';
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body>Login</body></html>'
    }));
    await page.route('http://survey.test/survey/1/delete', async (route) => {
        ajaxHeader = route.request().headers()['x-requested-with'] || '';
        await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Требуется авторизация. Выполните вход снова.' })
        });
    });

    await page.goto('http://survey.test/survey');
    await page.setContent(`
        <input name="__RequestVerificationToken" value="test-antiforgery-token">
    `);
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/core/antiforgery.js')
    });

    const redirectedToLogin = page.waitForURL('http://survey.test/');
    await page.evaluate(() => {
        void fetch('/survey/1/delete', { method: 'POST' });
    });
    await redirectedToLogin;

    expect(ajaxHeader).toBe('XMLHttpRequest');
    await expect(page.getByText('Login', { exact: true })).toBeVisible();
});

test('HTML-ответ не используется как текст уведомления', async ({ page }) => {
    await page.goto('about:blank');
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/core/app-http.js')
    });

    const message = await page.evaluate(() => window.AppHttp.readResponseMessage(
        new Response('<!DOCTYPE html><html><body>Авторизация</body></html>', {
            status: 200,
            headers: { 'Content-Type': 'text/html; charset=utf-8' }
        }),
        'Не удалось удалить анкету.'
    ));

    expect(message).toBe('Не удалось удалить анкету.');
});

test('ошибка входа 401 остаётся на странице авторизации', async ({ page }) => {
    let ajaxHeader = '';
    await page.route('http://survey.test/**', (route) => route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: '<!doctype html><html><body>Login</body></html>'
    }));
    await page.route('http://survey.test/auth/login', async (route) => {
        ajaxHeader = route.request().headers()['x-requested-with'] || '';
        await route.fulfill({
            status: 401,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Неверный логин или пароль.' })
        });
    });

    await page.goto('http://survey.test/');
    await page.addScriptTag({
        path: path.join(projectRoot, 'wwwroot/js/core/antiforgery.js')
    });

    const result = await page.evaluate(async () => {
        const response = await fetch('/auth/login', { method: 'POST' });
        return { status: response.status, body: await response.json() };
    });

    expect(result).toEqual({
        status: 401,
        body: { error: 'Неверный логин или пароль.' }
    });
    expect(ajaxHeader).toBe('XMLHttpRequest');
    await expect(page).toHaveURL('http://survey.test/');
});
