(() => {
if (window.__appTableSortLoaded) {
    return;
}

window.__appTableSortLoaded = true;

const TABLE_SELECTOR = [
    '[data-column-sort="true"]',
    '[data-role="main-table"]',
    '.surveys-table',
    '.users-table',
    '.organization-table',
    '.answers-page__signatures-table',
    '.answers-table',
    '.logs-table'
].join(', ');
const headerHandlers = new WeakMap();

function normalizeText(value) {
    return String(value || '')
        .replace(/\s+/g, ' ')
        .trim();
}

function isSortableHeader(header) {
    if (!header || header.dataset.sortable === 'false') {
        return false;
    }

    if (header.classList.contains('table-col--actions')) {
        return false;
    }

    if (header.querySelector('input, button, select, textarea')) {
        return false;
    }

    return Boolean(header.querySelector('a[href]')) && normalizeText(header.textContent).length > 0;
}

function activateSortLink(link) {
    if (!link?.href || link.getAttribute('href') === '#') {
        return;
    }

    const clickEvent = new MouseEvent('click', {
        bubbles: true,
        cancelable: true,
        view: window,
        button: 0
    });
    const wasCanceled = !link.dispatchEvent(clickEvent);

    if (!wasCanceled && !clickEvent.defaultPrevented) {
        window.location.assign(link.href);
    }
}

function bindServerSortHeader(header) {
    if (header.dataset.sortReady === 'true' || !isSortableHeader(header)) {
        return null;
    }

    const link = header.querySelector('a[href]');
    header.dataset.sortReady = 'true';
    header.classList.add('table-sortable');
    header.tabIndex = 0;
    header.setAttribute('role', 'link');

    if (!header.hasAttribute('aria-sort')) {
        header.setAttribute('aria-sort', 'none');
    }

    const onClick = (event) => {
        if (event.target.closest('a[href], button, input, select, textarea, label')) {
            return;
        }

        event.preventDefault();
        activateSortLink(link);
    };

    const onKeydown = (event) => {
        if (event.key !== 'Enter' && event.key !== ' ') {
            return;
        }

        event.preventDefault();
        activateSortLink(link);
    };

    header.addEventListener('click', onClick);
    header.addEventListener('keydown', onKeydown);
    const cleanup = () => {
        header.removeEventListener('click', onClick);
        header.removeEventListener('keydown', onKeydown);
        headerHandlers.delete(header);
        header.removeAttribute('data-sort-ready');
        header.removeAttribute('tabindex');
        header.removeAttribute('role');
        header.removeAttribute('aria-sort');
    };
    headerHandlers.set(header, cleanup);
    return cleanup;
}

function mountSortableTable(table) {
    if (
        !table
        || table.dataset.columnSortMounted === 'server'
        || table.dataset.columnSortMounted === 'skipped'
    ) {
        return;
    }

    const headers = Array.from(table.tHead?.querySelectorAll('th.table-sortable') || [])
        .filter(isSortableHeader);

    if (headers.length === 0) {
        return null;
    }

    const cleanups = headers
        .map(bindServerSortHeader)
        .filter((cleanup) => typeof cleanup === 'function');
    table.dataset.columnSortMounted = 'server';

    return () => {
        cleanups.forEach((cleanup) => cleanup());
        table.removeAttribute('data-column-sort-mounted');
    };
}

function mountSortableTables(root = document) {
    if (!root || typeof root.querySelectorAll !== 'function') {
        return;
    }

    if (root.matches?.(TABLE_SELECTOR)) {
        mountSortableTable(root);
    }

    root.querySelectorAll(TABLE_SELECTOR).forEach((table) => {
        mountSortableTable(table);
    });
}

window.mountSortableTables = mountSortableTables;

if (window.AppPageLifecycle?.register) {
    window.AppPageLifecycle.register(
        'table-server-sort',
        TABLE_SELECTOR,
        mountSortableTable
    );
} else if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => mountSortableTables(document), { once: true });
} else {
    mountSortableTables(document);
}
})();
