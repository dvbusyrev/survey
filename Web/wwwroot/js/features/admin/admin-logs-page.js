(function () {
    let cachedBootstrapText = '';
    let cachedLogsMap = new Map();
    let cachedDetailsPromises = new Map();
    let descriptionSortFrame = 0;

    function syncDescriptionSortMarker() {
        if (descriptionSortFrame) {
            window.cancelAnimationFrame(descriptionSortFrame);
        }

        descriptionSortFrame = window.requestAnimationFrame(() => {
            descriptionSortFrame = 0;

            const table = document.querySelector('.logs-table');
            const header = table?.querySelector('th.table-col--description.table-sortable');
            const cells = table ? Array.from(table.querySelectorAll('tbody td.table-col--description')) : [];
            if (!table || !header || cells.length === 0) {
                return;
            }

            const descriptionCell = cells.find((cell) => {
                const text = (cell.textContent || '').trim();
                return text.length > 0 && cell.getClientRects().length > 0;
            });
            if (!descriptionCell) {
                return;
            }

            const range = document.createRange();
            range.selectNodeContents(descriptionCell);
            const textRects = Array.from(range.getClientRects());
            range.detach();

            const textRect = textRects
                .filter((rect) => rect.width > 0 && rect.height > 0)
                .reduce((rightmostRect, rect) => (
                    !rightmostRect || rect.right > rightmostRect.right ? rect : rightmostRect
                ), null);
            if (!textRect) {
                return;
            }

            const headerRect = header.getBoundingClientRect();
            const headerStyle = window.getComputedStyle(header);
            const headerPaddingLeft = Number.parseFloat(headerStyle.paddingLeft) || 0;
            const markerGap = 10;
            const markerWidth = 12;
            const minMarkerLeft = headerPaddingLeft;
            const maxMarkerLeft = Math.max(minMarkerLeft, headerRect.width - markerWidth);
            const markerLeft = Math.min(
                Math.max(textRect.right - headerRect.left + markerGap, minMarkerLeft),
                maxMarkerLeft
            );

            header.style.setProperty('--logs-description-sort-left', `${Math.ceil(markerLeft)}px`);
        });
    }

    function readLogsMap() {
        const bootstrapNode = document.getElementById('logs-page-bootstrap');
        const rawText = bootstrapNode?.textContent?.trim() || '';
        if (!rawText) {
            cachedBootstrapText = '';
            cachedLogsMap = new Map();
            return cachedLogsMap;
        }

        if (rawText === cachedBootstrapText) {
            return cachedLogsMap;
        }

        try {
            const items = JSON.parse(rawText);
            cachedLogsMap = new Map(
                (Array.isArray(items) ? items : [])
                    .map((item) => [Number(item.id || 0), item])
                    .filter(([id]) => Number.isFinite(id) && id > 0)
            );
            cachedBootstrapText = rawText;
        } catch (error) {
            console.error('Не удалось разобрать данные журнала событий:', error);
            cachedBootstrapText = rawText;
            cachedLogsMap = new Map();
        }

        return cachedLogsMap;
    }

    function getLogEntry(logId) {
        if (!Number.isFinite(logId) || logId <= 0) {
            return null;
        }

        return readLogsMap().get(logId) || null;
    }

    function hasDetails(entry) {
        return typeof entry?.extraDataJson === 'string' && entry.extraDataJson.trim().length > 0;
    }

    function buildLogDetailsUrl(logId) {
        const url = new URL(`/event-log/details/${encodeURIComponent(String(logId))}`, window.location.origin);
        const currentParams = new URLSearchParams(window.location.search);

        ['page', 'sortBy', 'sortDirection'].forEach((name) => {
            const value = currentParams.get(name);
            if (value) {
                url.searchParams.set(name, value);
            }
        });

        return url.toString();
    }

    async function loadLogEntryDetails(logId) {
        const cachedEntry = getLogEntry(logId);
        if (!cachedEntry || hasDetails(cachedEntry)) {
            return cachedEntry;
        }

        if (cachedDetailsPromises.has(logId)) {
            return cachedDetailsPromises.get(logId);
        }

        const promise = fetch(buildLogDetailsUrl(logId), {
            cache: 'no-store',
            headers: {
                'Accept': 'application/json'
            }
        })
            .then(async (response) => {
                if (!response.ok) {
                    const payload = await response.json().catch(() => ({}));
                    throw new Error(payload.message || 'Не удалось загрузить событие');
                }

                return response.json();
            })
            .then((details) => {
                const mergedEntry = {
                    ...cachedEntry,
                    ...details
                };
                readLogsMap().set(logId, mergedEntry);
                return mergedEntry;
            })
            .finally(() => {
                cachedDetailsPromises.delete(logId);
            });

        cachedDetailsPromises.set(logId, promise);
        return promise;
    }

    function setText(container, role, value) {
        const target = container.querySelector(`[data-role="${role}"]`);
        if (target) {
            target.textContent = value && String(value).trim().length > 0
                ? String(value).trim()
                : '—';
        }
    }

    function parseExtraData(entry) {
        const rawJson = typeof entry?.extraDataJson === 'string'
            ? entry.extraDataJson.trim()
            : '';

        if (!rawJson) {
            return null;
        }

        try {
            return JSON.parse(rawJson);
        } catch (error) {
            console.error('Не удалось разобрать details журнала событий:', error);
            return null;
        }
    }

    function isRecordObject(value) {
        return Boolean(value && typeof value === 'object' && !Array.isArray(value));
    }

    function formatValue(value) {
        if (value == null) {
            return 'пусто';
        }

        if (typeof value === 'string') {
            const trimmed = value.trim();
            return trimmed.length > 0 ? trimmed : 'пусто';
        }

        if (typeof value === 'object') {
            try {
                return JSON.stringify(value);
            } catch (error) {
                return String(value);
            }
        }

        return String(value);
    }

    function getOperation(extraData) {
        return typeof extraData?.operation === 'string'
            ? extraData.operation.toUpperCase()
            : '';
    }

    function getSourceTable(extraData, entry) {
        return formatValue(extraData?.source_table || extraData?.source_table_name || entry?.targetType);
    }

    function getRecordRows(extraData) {
        const operation = getOperation(extraData);
        const currentRecord = isRecordObject(extraData?.new_row_data)
            ? extraData.new_row_data
            : (isRecordObject(extraData?.row_data) ? extraData.row_data : null);
        const previousRecord = isRecordObject(extraData?.old_row_data)
            ? extraData.old_row_data
            : (isRecordObject(extraData?.previous_row_data) ? extraData.previous_row_data : null);
        const deletedRecord = isRecordObject(extraData?.old_row_data)
            ? extraData.old_row_data
            : (isRecordObject(extraData?.row_data) ? extraData.row_data : null);

        if (operation === 'UPDATE') {
            return [
                previousRecord ? { type: 'old', data: previousRecord } : null,
                currentRecord ? { type: 'new', data: currentRecord } : null
            ].filter(Boolean);
        }

        if (operation === 'DELETE') {
            return deletedRecord ? [{ type: 'old', data: deletedRecord }] : [];
        }

        return currentRecord ? [{ type: 'new', data: currentRecord }] : [];
    }

    function normalizeColumnList(value) {
        if (!Array.isArray(value)) {
            return [];
        }

        const seenColumns = new Set();
        return value
            .map((columnName) => String(columnName || '').trim())
            .filter((columnName) => {
                const normalizedColumnName = columnName.toLowerCase();
                if (!columnName || seenColumns.has(normalizedColumnName)) {
                    return false;
                }

                seenColumns.add(normalizedColumnName);
                return true;
            });
    }

    function mergeColumnOrder(target, nextColumns) {
        const knownColumns = new Set(target.map((columnName) => columnName.toLowerCase()));
        nextColumns.forEach((columnName) => {
            const normalizedColumnName = columnName.toLowerCase();
            if (knownColumns.has(normalizedColumnName)) {
                return;
            }

            knownColumns.add(normalizedColumnName);
            target.push(columnName);
        });
    }

    function collectColumns(recordRows, columnOrder) {
        const columns = [];
        const columnSet = new Set();

        normalizeColumnList(columnOrder).forEach((columnName) => {
            columns.push(columnName);
            columnSet.add(columnName.toLowerCase());
        });

        recordRows.forEach((recordRow) => {
            Object.keys(recordRow.data || {}).forEach((columnName) => {
                const normalizedColumnName = columnName.toLowerCase();
                if (columnSet.has(normalizedColumnName)) {
                    return;
                }

                columnSet.add(normalizedColumnName);
                columns.push(columnName);
            });
        });

        return columns;
    }

    function normalizeRecordForKey(value) {
        if (Array.isArray(value)) {
            return value.map(normalizeRecordForKey);
        }

        if (isRecordObject(value)) {
            return Object.keys(value)
                .sort((left, right) => left.localeCompare(right))
                .reduce((normalized, key) => {
                    normalized[key] = normalizeRecordForKey(value[key]);
                    return normalized;
                }, {});
        }

        return value ?? null;
    }

    function getRecordRowKey(recordRow) {
        return JSON.stringify({
            type: recordRow.type,
            data: normalizeRecordForKey(recordRow.data)
        });
    }

    function getRecordTableItems(extraData) {
        if (Array.isArray(extraData?.items) && extraData.items.length > 0) {
            return extraData.items.filter(isRecordObject);
        }

        return isRecordObject(extraData) ? [extraData] : [];
    }

    function buildRecordTableGroups(extraData, entry) {
        const groups = [];
        const groupMap = new Map();

        getRecordTableItems(extraData).forEach((item) => {
            const recordRows = getRecordRows(item);
            if (recordRows.length === 0) {
                return;
            }

            const sourceTable = getSourceTable(item, entry);
            let group = groupMap.get(sourceTable);
            if (!group) {
                group = {
                    sourceTable,
                    columnOrder: [],
                    recordRows: [],
                    recordRowKeys: new Set()
                };
                groupMap.set(sourceTable, group);
                groups.push(group);
            }

            mergeColumnOrder(group.columnOrder, normalizeColumnList(item.column_order));

            recordRows.forEach((recordRow) => {
                const recordRowKey = getRecordRowKey(recordRow);
                if (group.recordRowKeys.has(recordRowKey)) {
                    return;
                }

                group.recordRowKeys.add(recordRowKey);
                group.recordRows.push(recordRow);
            });
        });

        return groups;
    }

    function appendCell(row, tagName, text) {
        const cell = document.createElement(tagName);
        cell.textContent = text;
        row.appendChild(cell);
        return cell;
    }

    function renderEmptyRecordTable(host) {
        const empty = document.createElement('p');
        empty.className = 'logs-modal__empty';
        empty.textContent = 'Данные записи не найдены.';
        host.appendChild(empty);
    }

    function appendRecordTable(host, sourceTable, recordRows, columnOrder) {
        const columns = collectColumns(recordRows, columnOrder);
        if (recordRows.length === 0 || columns.length === 0) {
            return false;
        }

        const table = document.createElement('table');
        table.className = 'logs-modal__record-table';

        const caption = document.createElement('caption');
        caption.textContent = sourceTable;
        table.appendChild(caption);

        const thead = document.createElement('thead');
        const headerRow = document.createElement('tr');
        columns.forEach((columnName) => appendCell(headerRow, 'th', columnName));
        thead.appendChild(headerRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        recordRows.forEach((recordRow) => {
            const row = document.createElement('tr');
            row.className = recordRow.type === 'old'
                ? 'logs-modal__record-row logs-modal__record-row--old'
                : 'logs-modal__record-row logs-modal__record-row--new';
            columns.forEach((columnName) => appendCell(row, 'td', formatValue(recordRow.data?.[columnName])));
            tbody.appendChild(row);
        });
        table.appendChild(tbody);
        host.appendChild(table);

        return true;
    }

    function renderRecordTable(modal, extraData, entry) {
        const host = modal.querySelector('[data-role="log-record-table"]');
        if (!host) {
            return;
        }

        host.replaceChildren();

        const renderedCount = buildRecordTableGroups(extraData, entry)
            .reduce((count, group) => {
                return appendRecordTable(host, group.sourceTable, group.recordRows, group.columnOrder)
                    ? count + 1
                    : count;
            }, 0);

        if (renderedCount === 0) {
            renderEmptyRecordTable(host);
        }
    }

    function getEventTitle(entry, extraData) {
        const eventType = typeof extraData?.operation_name === 'string' && extraData.operation_name.trim()
            ? extraData.operation_name.trim()
            : entry?.eventType;

        return eventType && eventType !== '—'
            ? `${eventType} записи`
            : 'Событие';
    }

    function resizeLogModal(modal) {
        const content = modal.querySelector('.modal-content');
        const body = modal.querySelector('.modal-body');
        if (!content || !body) {
            return;
        }

        const tables = Array.from(modal.querySelectorAll('.logs-modal__record-table'));
        if (tables.length === 0) {
            content.style.removeProperty('--logs-modal-width');
            return;
        }

        const bodyStyles = window.getComputedStyle(body);
        const horizontalBodyPadding = (Number.parseFloat(bodyStyles.paddingLeft) || 0)
            + (Number.parseFloat(bodyStyles.paddingRight) || 0);
        const preferredTableWidth = Math.max(...tables.map((table) => table.scrollWidth));
        const preferredWidth = Math.max(720, preferredTableWidth + horizontalBodyPadding + 2);
        const maxWidth = Math.max(320, window.innerWidth - 32);

        content.style.setProperty('--logs-modal-width', `${Math.min(preferredWidth, maxWidth)}px`);
    }

    async function openLogEntryModal(logId) {
        let entry;
        try {
            entry = await loadLogEntryDetails(logId);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Не удалось загрузить событие';
            console.error('Не удалось загрузить событие журнала:', error);
            if (typeof window.showNotification === 'function') {
                window.showNotification(message, false);
            }
            return;
        }

        const modal = document.getElementById('logEntryModal');
        if (!entry || !modal) {
            return;
        }

        const extraData = parseExtraData(entry);

        setText(modal, 'log-modal-subtitle', getEventTitle(entry, extraData));
        setText(modal, 'log-date', entry.date);
        setText(modal, 'log-user', entry.user);
        setText(modal, 'log-event', entry.eventType);
        setText(modal, 'log-record-id', extraData?.target_id);

        renderRecordTable(modal, extraData, entry);

        if (typeof window.showSiteModal === 'function') {
            window.showSiteModal(modal);
            window.requestAnimationFrame(() => resizeLogModal(modal));
        }
    }

    window.openLogEntryModalByTrigger = function openLogEntryModalByTrigger(element) {
        const logId = Number(element?.dataset?.logId || 0);
        openLogEntryModal(logId);
    };

    window.openLogEntryModalByRow = function openLogEntryModalByRow(element) {
        const logId = Number(element?.dataset?.logId || 0);
        openLogEntryModal(logId);
    };

    document.addEventListener('dblclick', (event) => {
        const row = event.target.closest('.logs-table tbody tr[data-log-id]');
        if (!row) {
            return;
        }

        openLogEntryModal(Number(row.dataset.logId || 0));
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Enter') {
            return;
        }

        const row = event.target.closest('.logs-table tbody tr[data-log-id]');
        if (!row) {
            return;
        }

        openLogEntryModal(Number(row.dataset.logId || 0));
    });

    window.addEventListener('resize', () => {
        syncDescriptionSortMarker();

        const modal = document.getElementById('logEntryModal');
        if (modal?.classList.contains('modal--visible')) {
            resizeLogModal(modal);
        }
    });

    window.addEventListener('load', syncDescriptionSortMarker, { once: true });
    if (document.fonts?.ready) {
        document.fonts.ready.then(syncDescriptionSortMarker).catch(() => {});
    }

    syncDescriptionSortMarker();
})();
