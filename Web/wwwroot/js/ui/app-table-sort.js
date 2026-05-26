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

const collator = new Intl.Collator('ru', {
    numeric: true,
    sensitivity: 'base'
});

function normalizeText(value) {
    return String(value || '')
        .replace(/\s+/g, ' ')
        .trim();
}

function extractCellValue(cell) {
    return normalizeText(cell?.dataset?.sortValue ?? cell?.textContent ?? '');
}

function parseDateValue(value) {
    const normalized = normalizeText(value);
    if (!normalized) {
        return null;
    }

    const isoMatch = normalized.match(/\b(\d{4})-(\d{2})-(\d{2})\b/);
    if (isoMatch) {
        const [, year, month, day] = isoMatch;
        const timestamp = new Date(Number(year), Number(month) - 1, Number(day)).getTime();
        return Number.isNaN(timestamp) ? null : timestamp;
    }

    const localizedMatch = normalized.match(/\b(\d{2})\.(\d{2})\.(\d{4})(?:\s+(\d{2}):(\d{2})(?::(\d{2}))?)?\b/);
    if (!localizedMatch) {
        return null;
    }

    const [, day, month, year, hours = '0', minutes = '0', seconds = '0'] = localizedMatch;
    const timestamp = new Date(
        Number(year),
        Number(month) - 1,
        Number(day),
        Number(hours),
        Number(minutes),
        Number(seconds)
    ).getTime();

    return Number.isNaN(timestamp) ? null : timestamp;
}

function parseNumberValue(value) {
    const normalized = normalizeText(value);
    if (!normalized) {
        return null;
    }

    const numberMatch = normalized.match(/-?\d+(?:[.,]\d+)?/);
    if (!numberMatch) {
        return null;
    }

    const parsed = Number(numberMatch[0].replace(',', '.'));
    return Number.isFinite(parsed) ? parsed : null;
}

function isEmptyRow(row) {
    return Boolean(
        row?.querySelector?.('.table-empty-cell')
        || row?.dataset?.role === 'user-survey-empty-row'
        || row?.dataset?.role === 'user-survey-filter-empty-row'
        || row?.id === 'none_result'
    );
}

function isComplexTable(table) {
    const bodyRows = Array.from(table.tBodies[0]?.rows || []);
    return bodyRows.some((row) => {
        if (isEmptyRow(row) || row.dataset.sortIgnore === 'true') {
            return false;
        }

        if (Array.from(row.cells).some((cell) => cell.rowSpan > 1)) {
            return true;
        }

        return row.cells.length === 1
            && row.cells[0].colSpan > 1
            && !row.querySelector('.table-empty-cell');
    });
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

    return normalizeText(header.textContent).length > 0;
}

function getHeaderColumnIndex(header) {
    if (!header?.parentElement) {
        return -1;
    }

    return Array.from(header.parentElement.children).indexOf(header);
}

function resolveColumnSortType(rows, columnIndex) {
    const values = rows
        .map((row) => extractCellValue(row.cells[columnIndex]))
        .filter(Boolean);

    if (values.length === 0) {
        return 'text';
    }

    if (values.every((value) => parseDateValue(value) !== null)) {
        return 'date';
    }

    if (values.every((value) => parseNumberValue(value) !== null)) {
        return 'number';
    }

    return 'text';
}

function compareValues(leftValue, rightValue, type) {
    if (!leftValue && !rightValue) {
        return 0;
    }

    if (!leftValue) {
        return 1;
    }

    if (!rightValue) {
        return -1;
    }

    if (type === 'date') {
        return parseDateValue(leftValue) - parseDateValue(rightValue);
    }

    if (type === 'number') {
        return parseNumberValue(leftValue) - parseNumberValue(rightValue);
    }

    return collator.compare(leftValue, rightValue);
}

function updateHeaderStates(table, activeHeader, direction) {
    table.querySelectorAll('thead th.table-sortable').forEach((header) => {
        const isActive = header === activeHeader && Boolean(direction);
        header.dataset.sortDirection = isActive ? direction : '';
        header.setAttribute('aria-sort', isActive
            ? (direction === 'asc' ? 'ascending' : 'descending')
            : 'none');
        header.classList.toggle('is-sorted', isActive);
    });
}

function ensureOriginalRowOrder(table, rows) {
    if (!table || !Array.isArray(rows)) {
        return;
    }

    let nextIndex = Number.parseInt(table.dataset.originalSortOrderSize || '0', 10);
    if (!Number.isFinite(nextIndex) || nextIndex < 0) {
        nextIndex = 0;
    }

    rows.forEach((row) => {
        if (!row.dataset.originalSortIndex) {
            row.dataset.originalSortIndex = String(nextIndex);
            nextIndex += 1;
        }
    });

    table.dataset.originalSortOrderSize = String(nextIndex);
}

function restoreOriginalOrder(table) {
    const tbody = table.tBodies[0];
    if (!tbody) {
        return;
    }

    const rows = Array.from(tbody.rows);
    ensureOriginalRowOrder(table, rows);

    rows.sort((left, right) => {
        const leftIndex = Number.parseInt(left.dataset.originalSortIndex || '0', 10);
        const rightIndex = Number.parseInt(right.dataset.originalSortIndex || '0', 10);
        return leftIndex - rightIndex;
    });

    const fragment = document.createDocumentFragment();
    rows.forEach((row) => fragment.appendChild(row));
    tbody.appendChild(fragment);
}

function sortTable(table, header) {
    const tbody = table.tBodies[0];
    const columnIndex = getHeaderColumnIndex(header);
    if (!tbody || columnIndex < 0) {
        return;
    }

    const rows = Array.from(tbody.rows);
    ensureOriginalRowOrder(table, rows);
    const stickyRows = [];
    const sortableRows = [];

    rows.forEach((row, originalIndex) => {
        if (isEmptyRow(row) || row.dataset.sortIgnore === 'true') {
            stickyRows.push(row);
            return;
        }

        sortableRows.push({
            row,
            originalIndex,
            value: extractCellValue(row.cells[columnIndex])
        });
    });

    if (sortableRows.length <= 1) {
        return;
    }

    const currentDirection = header.dataset.sortDirection || '';
    const direction = currentDirection === ''
        ? 'asc'
        : (currentDirection === 'asc' ? 'desc' : '');

    if (!direction) {
        restoreOriginalOrder(table);
        updateHeaderStates(table, null, '');
        return;
    }

    const multiplier = direction === 'asc' ? 1 : -1;
    const sortType = resolveColumnSortType(sortableRows.map((item) => item.row), columnIndex);

    sortableRows.sort((left, right) => {
        const comparison = compareValues(left.value, right.value, sortType);
        if (comparison !== 0) {
            return comparison * multiplier;
        }

        return left.originalIndex - right.originalIndex;
    });

    const fragment = document.createDocumentFragment();
    sortableRows.forEach((item) => fragment.appendChild(item.row));
    stickyRows.forEach((row) => fragment.appendChild(row));
    tbody.appendChild(fragment);

    updateHeaderStates(table, header, direction);
}

function bindHeader(table, header) {
    if (header.dataset.sortReady === 'true') {
        return;
    }

    header.dataset.sortReady = 'true';
    header.classList.add('table-sortable');
    header.tabIndex = 0;
    header.setAttribute('aria-sort', 'none');

    const serverSortLink = header.querySelector('a[href]');
    if (serverSortLink) {
        header.setAttribute('role', 'link');

        const activateLink = () => {
            const handledByPage = !serverSortLink.dispatchEvent(new MouseEvent('click', {
                bubbles: true,
                cancelable: true,
                view: window,
                button: 0
            }));

            if (!handledByPage) {
                window.location.assign(serverSortLink.href);
            }
        };

        header.addEventListener('click', (event) => {
            if (event.target.closest('a[href], button, input, select, textarea, label')) {
                return;
            }

            event.preventDefault();
            activateLink();
        });

        header.addEventListener('keydown', (event) => {
            if (event.key !== 'Enter' && event.key !== ' ') {
                return;
            }

            event.preventDefault();
            activateLink();
        });
        return;
    }

    header.setAttribute('role', 'button');

    const activateSort = () => sortTable(table, header);

    header.addEventListener('click', activateSort);
    header.addEventListener('keydown', (event) => {
        if (event.key !== 'Enter' && event.key !== ' ') {
            return;
        }

        event.preventDefault();
        activateSort();
    });
}

function mountSortableTable(table) {
    if (!table || table.dataset.columnSortMounted === 'true' || table.dataset.disableColumnSort === 'true') {
        return;
    }

    if (!table.tHead || !table.tBodies.length || isComplexTable(table)) {
        table.dataset.columnSortMounted = 'skipped';
        return;
    }

    const headers = Array.from(table.tHead.querySelectorAll('th')).filter(isSortableHeader);
    if (headers.length === 0) {
        table.dataset.columnSortMounted = 'skipped';
        return;
    }

    headers.forEach((header) => bindHeader(table, header));
    table.dataset.columnSortMounted = 'true';
}

function mountSortableTables(root = document) {
    if (!root || typeof root.querySelectorAll !== 'function') {
        return;
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
