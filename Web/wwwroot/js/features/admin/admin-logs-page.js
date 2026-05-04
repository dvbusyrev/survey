(function () {
    let cachedBootstrapText = '';
    let cachedLogsMap = new Map();

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
        const currentRecord = isRecordObject(extraData?.row_data) ? extraData.row_data : null;
        const previousRecord = isRecordObject(extraData?.previous_row_data) ? extraData.previous_row_data : null;

        if (operation === 'UPDATE') {
            return [
                previousRecord ? { type: 'old', data: previousRecord } : null,
                currentRecord ? { type: 'new', data: currentRecord } : null
            ].filter(Boolean);
        }

        if (operation === 'DELETE') {
            return currentRecord ? [{ type: 'old', data: currentRecord }] : [];
        }

        return currentRecord ? [{ type: 'new', data: currentRecord }] : [];
    }

    function collectColumns(recordRows) {
        const columns = [];
        const columnSet = new Set();

        recordRows.forEach((recordRow) => {
            Object.keys(recordRow.data || {}).forEach((columnName) => {
                if (columnSet.has(columnName)) {
                    return;
                }

                columnSet.add(columnName);
                columns.push(columnName);
            });
        });

        return columns;
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

    function renderRecordTable(modal, extraData, entry) {
        const host = modal.querySelector('[data-role="log-record-table"]');
        if (!host) {
            return;
        }

        host.replaceChildren();
        const sourceTable = getSourceTable(extraData, entry);

        const recordRows = getRecordRows(extraData);
        const columns = collectColumns(recordRows);
        if (recordRows.length === 0 || columns.length === 0) {
            renderEmptyRecordTable(host);
            return;
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
    }

    function getEventTitle(entry, extraData) {
        const eventType = typeof extraData?.operation_name === 'string' && extraData.operation_name.trim()
            ? extraData.operation_name.trim()
            : entry?.eventType;

        return eventType && eventType !== '—'
            ? `${eventType} записи`
            : 'Событие';
    }

    function openLogEntryModal(logId) {
        const entry = getLogEntry(logId);
        const modal = document.getElementById('logEntryModal');
        if (!entry || !modal) {
            return;
        }

        const extraData = parseExtraData(entry);
        const sourceTable = getSourceTable(extraData, entry);

        setText(modal, 'log-modal-subtitle', getEventTitle(entry, extraData));
        setText(modal, 'log-date', entry.date);
        setText(modal, 'log-user', entry.user);
        setText(modal, 'log-event', entry.eventType);
        setText(modal, 'log-record-id', extraData?.target_id);

        renderRecordTable(modal, extraData, entry);

        if (typeof window.showSiteModal === 'function') {
            window.showSiteModal(modal);
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
})();
