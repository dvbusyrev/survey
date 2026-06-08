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
        return;
    }

    const link = header.querySelector('a[href]');
    header.dataset.sortReady = 'true';
    header.classList.add('table-sortable');
    header.tabIndex = 0;
    header.setAttribute('role', 'link');

    if (!header.hasAttribute('aria-sort')) {
        header.setAttribute('aria-sort', 'none');
    }

    header.addEventListener('click', (event) => {
        if (event.target.closest('a[href], button, input, select, textarea, label')) {
            return;
        }

        event.preventDefault();
        activateSortLink(link);
    });

    header.addEventListener('keydown', (event) => {
        if (event.key !== 'Enter' && event.key !== ' ') {
            return;
        }

        event.preventDefault();
        activateSortLink(link);
    });
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
        table.dataset.columnSortMounted = 'skipped';
        return;
    }

    headers.forEach(bindServerSortHeader);
    table.dataset.columnSortMounted = 'server';
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

function observeTables() {
    if (!document.body || typeof MutationObserver === 'undefined') {
        return;
    }

    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (!(node instanceof Element)) {
                    return;
                }

                if (node.matches?.(TABLE_SELECTOR)) {
                    mountSortableTable(node);
                }

                mountSortableTables(node);
            });
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
}

window.mountSortableTables = mountSortableTables;

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        mountSortableTables(document);
        observeTables();
    }, { once: true });
} else {
    mountSortableTables(document);
    observeTables();
}
})();
