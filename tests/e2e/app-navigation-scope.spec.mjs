import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '@playwright/test';

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

test('стили бокового меню не поднимают пагинацию над фильтрами', async ({ page }) => {
    await page.setContent(`
        <div id="chrome-navigation">
            <nav class="admin-nav">
                <ul><li><a class="nav-link" href="#">Анкеты</a></li></ul>
            </nav>
        </div>
        <div class="app-pagination">
            <nav class="app-pagination__nav" aria-label="Навигация по страницам">
                <a href="#">2</a>
            </nav>
        </div>
    `);
    await page.addStyleTag({
        content: ':root { --page-gap: 12px; --text-main: #111; --app-nav-panel-radius: 8px; }'
    });
    await page.addStyleTag({ path: path.join(projectRoot, 'wwwroot/css/shared/app-theme.css') });
    await page.addStyleTag({ path: path.join(projectRoot, 'wwwroot/css/shared/app-shell.css') });

    await expect(page.locator('.admin-nav')).toHaveCSS('position', 'sticky');
    await expect(page.locator('.admin-nav')).toHaveCSS('z-index', '2400');
    await expect(page.locator('.app-pagination__nav')).toHaveCSS('position', 'static');
    await expect(page.locator('.app-pagination__nav')).toHaveCSS('z-index', 'auto');
});
