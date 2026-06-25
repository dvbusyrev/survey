(() => {
if (window.__appTablePaginationLoaded) {
    return;
}

window.__appTablePaginationLoaded = true;

const DEFAULT_PAGE_SIZE = 10;
const MAX_VISIBLE_PAGES = 5;
const TABLE_SELECTOR = '[data-enable-local-table-pagination="true"]';
const AUXILIARY_HIDDEN_CLASSES = [
    'is-hidden',
    'is-hidden-by-date',
    'is-hidden-by-organization',
    'is-hidden-by-survey-name'
];
const instances = new WeakMap();

function isAuxiliaryRow(row) {
    return Boolean(
        row?.querySelector?.('.table-empty-cell')
        || row?.dataset?.role === 'survey-filter-empty-row'
        || row?.dataset?.role === 'user-survey-empty-row'
        || row?.dataset?.role === 'user-survey-filter-empty-row'
        || row?.id === 'none_result'
    );
}

function isExternallyHidden(row) {
    if (!row) {
        return true;
    }

    if (row.hidden || row.style.display === 'none') {
        return true;
    }

    return AUXILIARY_HIDDEN_CLASSES.some((className) => row.classList.contains(className));
}

function getPageSize(table) {
    const parsedValue = Number.parseInt(table?.dataset?.paginationPageSize || '', 10);
    return Number.isFinite(parsedValue) && parsedValue > 0
        ? parsedValue
        : DEFAULT_PAGE_SIZE;
}

function getPaginationAnchor(table) {
    return table?.closest('.table-responsive') || table;
}

function scrollTableIntoView(instance) {
    const target = instance?.table?.closest('.table-responsive') || instance?.table;
    if (!target) {
        return;
    }

    target.scrollIntoView({
        block: 'start',
        behavior: 'auto'
    });
}

function getPaginationLabel(table) {
    const explicitLabel = String(table?.dataset?.paginationLabel || '').trim();
    if (explicitLabel) {
        return explicitLabel;
    }

    const page = table?.closest('.app-page');
    const pageTitle = page?.querySelector('.app-page__title, .page-title, h1, h2');
    const titleText = String(pageTitle?.textContent || '').trim();

    return titleText
        ? `Навигация по страницам: ${titleText}`
        : 'Навигация по страницам таблицы';
}

function createPaginationHost(anchor) {
    const host = document.createElement('div');
    host.className = 'app-pagination';
    host.dataset.role = 'table-pagination';
    host.hidden = true;

    const nav = document.createElement('nav');
    nav.className = 'app-pagination__nav';
    host.appendChild(nav);

    anchor.insertAdjacentElement('afterend', host);
    return host;
}

function buildPaginationItems(currentPage, totalPages) {
    const items = [];
    let startPage = 1;

    if (totalPages > MAX_VISIBLE_PAGES) {
        startPage = currentPage - 2;
        if (startPage < 1) {
            startPage = 1;
        }

        const maxStartPage = totalPages - MAX_VISIBLE_PAGES + 1;
        if (startPage > maxStartPage) {
            startPage = maxStartPage;
        }
    }

    const endPage = Math.min(totalPages, startPage + MAX_VISIBLE_PAGES - 1);

    if (totalPages > MAX_VISIBLE_PAGES && startPage > 1) {
        items.push({
            label: 'В начало',
            page: 1,
            isAction: true,
            isCurrent: false
        });
    }

    for (let page = startPage; page <= endPage; page += 1) {
        items.push({
            label: String(page),
            page,
            isAction: false,
            isCurrent: page === currentPage
        });
    }

    if (currentPage < totalPages) {
        items.push({
            label: 'Дальше',
            page: currentPage + 1,
            isAction: true,
            isCurrent: false
        });
    }

    return items;
}

function createPaginationControl(item) {
    if (item.isCurrent) {
        const current = document.createElement('span');
        current.className = 'app-pagination__page-link app-pagination__page-link--current';
        current.setAttribute('aria-current', 'page');
        current.textContent = item.label;
        return current;
    }

    const button = document.createElement('button');
    button.type = 'button';
    button.className = item.isAction
        ? 'app-pagination__action'
        : 'app-pagination__page-link';
    button.dataset.role = 'table-pagination-page';
    button.dataset.page = String(item.page);
    button.textContent = item.label;
    return button;
}

function scheduleRender(instance) {
    if (!instance || instance.renderScheduled) {
        return;
    }

    instance.renderScheduled = true;
    window.requestAnimationFrame(() => {
        instance.renderScheduled = false;
        renderPagination(instance);
    });
}

function renderPagination(instance) {
    if (!instance?.table?.isConnected || !instance?.tbody?.isConnected || !instance.host?.isConnected) {
        return;
    }

    const rows = Array.from(instance.tbody.rows || []);
    const dataRows = rows.filter((row) => !isAuxiliaryRow(row));
    const visibleRows = dataRows.filter((row) => !isExternallyHidden(row));
    const totalPages = Math.max(1, Math.ceil(visibleRows.length / instance.pageSize));

    if (visibleRows.length <= instance.pageSize) {
        instance.currentPage = 1;
        dataRows.forEach((row) => row.removeAttribute('data-table-pagination-hidden'));
        instance.host.hidden = true;
        instance.nav.replaceChildren();
        return;
    }

    instance.currentPage = Math.min(Math.max(instance.currentPage, 1), totalPages);
    const startIndex = (instance.currentPage - 1) * instance.pageSize;
    const endIndex = startIndex + instance.pageSize;

    dataRows.forEach((row) => row.removeAttribute('data-table-pagination-hidden'));
    visibleRows.forEach((row, index) => {
        if (index < startIndex || index >= endIndex) {
            row.setAttribute('data-table-pagination-hidden', 'true');
        }
    });

    instance.host.hidden = false;
    instance.nav.setAttribute('aria-label', instance.label);
    instance.nav.replaceChildren(...buildPaginationItems(instance.currentPage, totalPages).map(createPaginationControl));
}

function handlePaginationClick(instance, event) {
    const button = event.target.closest('[data-role="table-pagination-page"]');
    if (!button || !instance.host.contains(button)) {
        return;
    }

    const targetPage = Number.parseInt(button.dataset.page || '', 10);
    if (!Number.isFinite(targetPage) || targetPage <= 0 || targetPage === instance.currentPage) {
        return;
    }

    event.preventDefault();
    instance.currentPage = targetPage;
    renderPagination(instance);
    scrollTableIntoView(instance);
}

function mountTablePagination(table) {
    if (!table || table.dataset.tablePaginationMounted === 'true' || table.dataset.disableTablePagination === 'true') {
        return;
    }

    const tbody = table.tBodies?.[0];
    const anchor = getPaginationAnchor(table);
    if (!tbody || !anchor) {
        return null;
    }

    const host = createPaginationHost(anchor);
    const nav = host.querySelector('.app-pagination__nav');
    const instance = {
        table,
        tbody,
        host,
        nav,
        label: getPaginationLabel(table),
        pageSize: getPageSize(table),
        currentPage: 1,
        renderScheduled: false,
        observer: null
    };

    const onHostClick = (event) => handlePaginationClick(instance, event);
    host.addEventListener('click', onHostClick);

    if (typeof MutationObserver !== 'undefined') {
        instance.observer = new MutationObserver((mutations) => {
            const hasMeaningfulMutation = mutations.some((mutation) => (
                mutation.type === 'childList'
                || (mutation.type === 'attributes' && mutation.attributeName !== 'data-table-pagination-hidden')
            ));

            if (hasMeaningfulMutation) {
                scheduleRender(instance);
            }
        });

        instance.observer.observe(tbody, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class', 'style', 'hidden']
        });
    }

    instances.set(table, instance);
    table.dataset.tablePaginationMounted = 'true';
    renderPagination(instance);

    return () => {
        instance.observer?.disconnect();
        host.removeEventListener('click', onHostClick);
        host.remove();
        instances.delete(table);
        table.removeAttribute('data-table-pagination-mounted');
        Array.from(tbody.rows || []).forEach((row) => {
            row.removeAttribute('data-table-pagination-hidden');
        });
    };
}

function mountTablePaginations(root = document) {
    if (!root || typeof root.querySelectorAll !== 'function') {
        return;
    }

    if (root.matches?.(TABLE_SELECTOR)) {
        mountTablePagination(root);
    }

    root.querySelectorAll(TABLE_SELECTOR).forEach((table) => {
        mountTablePagination(table);
    });
}

window.mountTablePaginations = mountTablePaginations;

if (window.AppPageLifecycle?.register) {
    window.AppPageLifecycle.register(
        'table-pagination',
        TABLE_SELECTOR,
        mountTablePagination
    );
} else if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => mountTablePaginations(document), { once: true });
} else {
    mountTablePaginations(document);
}
})();
