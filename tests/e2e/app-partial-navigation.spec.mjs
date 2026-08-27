import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function renderPage({ pageName, activeTab, title }) {
    return `<!doctype html>
<html lang="ru">
<head>
    <meta charset="utf-8">
    <title>${title}</title>
    <meta id="app-page-styles-start">
    <meta id="app-page-styles-end">
</head>
<body>
    <div id="global-antiforgery-token"><input name="__RequestVerificationToken" value="${pageName}-token"></div>
    <div id="layout-chrome-context" data-user-role="admin" data-active-tab="${activeTab}" hidden></div>
    <div class="app-shell" data-app-shell="admin">
        <header id="chrome-header">Постоянный заголовок</header>
        <div id="chrome-navigation">
            <a href="/survey" data-tab="get_surveys" class="${activeTab === 'get_surveys' ? 'active' : ''}">Анкеты</a>
            <a href="/users" data-tab="get_users" class="${activeTab === 'get_users' ? 'active' : ''}">Пользователи</a>
            <a href="/organizations" data-tab="get_organization" class="${activeTab === 'get_organization' ? 'active' : ''}">Организации</a>
        </div>
        <main id="content_admin">
            <section id="default_content" data-page="${pageName}">${title}</section>
        </main>
        <footer id="chrome-footer">Постоянный подвал</footer>
    </div>
    <div id="app-page-scripts" hidden>
        <script>
            window.AppPageLifecycle?.register(
                '${pageName}-controller',
                '[data-page="${pageName}"]',
                function (page) {
                    page.dataset.controllerMounted = 'true';
                    return function () { page.dataset.controllerDisposed = 'true'; };
                }
            );
        </script>
    </div>
</body>
</html>`;
}

test('разделы меняются внутри постоянной оболочки без перезагрузки документа', async ({ page }) => {
    const pages = new Map([
        ['/survey', renderPage({ pageName: 'surveys-list', activeTab: 'get_surveys', title: 'Анкеты' })],
        ['/users', renderPage({ pageName: 'users-list', activeTab: 'get_users', title: 'Пользователи' })],
        ['/organizations', renderPage({ pageName: 'organization-list', activeTab: 'get_organization', title: 'Организации' })]
    ]);
    const pageErrors = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));
    await page.route('http://survey.test/**', async (route) => {
        const requestUrl = new URL(route.request().url());
        const html = pages.get(requestUrl.pathname);
        if (!html) {
            await route.fulfill({ status: 404, body: 'Not found' });
            return;
        }
        await route.fulfill({ status: 200, contentType: 'text/html', body: html });
    });

    await page.goto('http://survey.test/survey');
    await page.addScriptTag({ path: path.join(projectRoot, 'wwwroot/js/core/app-page-lifecycle.js') });
    await page.addScriptTag({ path: path.join(projectRoot, 'wwwroot/js/core/app-navigation-router.js') });
    await page.locator('#chrome-header').evaluate((header) => {
        header.dataset.partialNavigationMarker = 'persistent';
    });

    await page.locator('#chrome-navigation a[href="/users"]').click();
    await expect(page).toHaveURL('http://survey.test/users');
    await expect(page.locator('[data-page="users-list"]')).toHaveAttribute('data-controller-mounted', 'true');
    await expect(page.locator('#chrome-header')).toHaveAttribute('data-partial-navigation-marker', 'persistent');
    await expect(page.locator('#chrome-navigation [data-tab="get_users"]')).toHaveClass(/active/);
    await expect(page.locator('input[name="__RequestVerificationToken"]')).toHaveValue('users-list-token');

    await page.locator('#chrome-navigation a[href="/organizations"]').click();
    await expect(page).toHaveURL('http://survey.test/organizations');
    await expect(page.locator('[data-page="organization-list"]')).toHaveAttribute('data-controller-mounted', 'true');
    await expect(page.locator('#chrome-header')).toHaveAttribute('data-partial-navigation-marker', 'persistent');

    await page.goBack();
    await expect(page).toHaveURL('http://survey.test/users');
    await expect(page.locator('[data-page="users-list"]')).toHaveAttribute('data-controller-mounted', 'true');
    await expect(page.locator('#chrome-header')).toHaveAttribute('data-partial-navigation-marker', 'persistent');
    expect(pageErrors).toEqual([]);
});
