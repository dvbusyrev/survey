(function () {
    let cachedBootstrapText = '';
    let cachedLogsMap = new Map();
    let cachedDetailsPromises = new Map();
    let descriptionSortFrame = 0;
    const ROW_TOOLTIP_OFFSET_X = 12;
    const ROW_TOOLTIP_OFFSET_Y = 14;
    const IGNORED_CHANGED_COLUMNS = new Set(['date_update', 'user_update']);
    const SENSITIVE_RECORD_COLUMNS = new Set([
        'password',
        'hash_password',
        'csp',
        'key_csp',
        'signature',
        'signed_content_base64',
        'recipient_emails',
        'smtp_password'
    ]);
    const BASE_RECORD_COLUMNS = {
        app_user: ['id_user', 'full_name'],
        organization: ['id_organization', 'organization_name'],
        survey: ['id_survey', 'name_survey'],
        survey_question: ['id_question', 'id_survey', 'question_order', 'question_text'],
        organization_survey: ['id_organization_survey', 'id_survey', 'id_organization'],
        answer: ['id_survey', 'name_survey', 'completed_by', 'id_organization', 'organization_name', 'id_answer', 'completion_date'],
        answer_item: ['id_survey', 'name_survey', 'completed_by', 'id_organization', 'organization_name', 'id_answer', 'question_order', 'question_text', 'rating', 'comment'],
        auto_creation_config: ['id_config', 'is_enabled'],
        survey_auto_creation_config: ['id_config', 'id_survey'],
        email_config: ['id_config', 'smtp_host', 'smtp_port'],
        theme_config: ['id_config', 'font_color', 'background_color']
    };
    let rowTooltip = null;
    let activeTooltipRow = null;
    let latestTooltipX = 0;
    let latestTooltipY = 0;
    let rowTooltipFrame = 0;

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

    function normalizeSourceTable(value) {
        return String(value || '').trim().toLowerCase();
    }

    function getEntryKey(logId, sourceTable) {
        return `${Number(logId) || 0}:${normalizeSourceTable(sourceTable)}`;
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
                    .map((item) => [getEntryKey(item.id, item.sourceTable), item])
                    .filter(([, item]) => {
                        const id = Number(item?.id || 0);
                        return Number.isFinite(id) && id > 0;
                    })
            );
            cachedBootstrapText = rawText;
        } catch (error) {
            console.error('Не удалось разобрать данные журнала событий:', error);
            cachedBootstrapText = rawText;
            cachedLogsMap = new Map();
        }

        return cachedLogsMap;
    }

    function getLogEntry(logId, sourceTable) {
        if (!Number.isFinite(logId) || logId <= 0) {
            return null;
        }

        const logsMap = readLogsMap();
        return logsMap.get(getEntryKey(logId, sourceTable))
            || Array.from(logsMap.values()).find((entry) => Number(entry?.id || 0) === logId)
            || null;
    }

    function hasDetails(entry) {
        return typeof entry?.extraDataJson === 'string' && entry.extraDataJson.trim().length > 0;
    }

    function buildLogDetailsUrl(logId, entry) {
        const url = new URL(`/logs/details/${encodeURIComponent(String(logId))}`, window.location.origin);
        const currentParams = new URLSearchParams(window.location.search);
        const sourceTable = String(entry?.sourceTable || '').trim();

        ['page', 'sortBy', 'sortDirection'].forEach((name) => {
            const value = currentParams.get(name);
            if (value) {
                url.searchParams.set(name, value);
            }
        });

        if (sourceTable) {
            url.searchParams.set('sourceTable', sourceTable);
        }

        return url.toString();
    }

    async function loadLogEntryDetails(logId, sourceTable) {
        const cachedEntry = getLogEntry(logId, sourceTable);
        if (!cachedEntry || hasDetails(cachedEntry)) {
            return cachedEntry;
        }

        const cacheKey = getEntryKey(logId, cachedEntry.sourceTable || sourceTable);
        if (cachedDetailsPromises.has(cacheKey)) {
            return cachedDetailsPromises.get(cacheKey);
        }

        const promise = fetch(buildLogDetailsUrl(logId, cachedEntry), {
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
                    ...details,
                    sourceTable: cachedEntry.sourceTable || sourceTable || details.sourceTable || ''
                };
                readLogsMap().set(getEntryKey(logId, mergedEntry.sourceTable), mergedEntry);
                return mergedEntry;
            })
            .finally(() => {
                cachedDetailsPromises.delete(cacheKey);
            });

        cachedDetailsPromises.set(cacheKey, promise);
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

    function formatRecordValue(columnName, value) {
        const normalizedColumnName = normalizeSourceTable(columnName);
        if (value == null) {
            return 'null';
        }

        if (normalizedColumnName === 'background_image_data_url') {
            return String(value || '').trim().length > 0
                ? 'Изображение загружено'
                : 'Нет изображения';
        }

        return formatValue(value);
    }

    function normalizeComparableValue(value) {
        if (value == null) {
            return null;
        }

        if (typeof value === 'string') {
            const trimmed = value.trim();
            return trimmed.length > 0 ? trimmed : null;
        }

        if (typeof value === 'object') {
            return normalizeRecordForKey(value);
        }

        return value;
    }

    function areValuesEqual(left, right) {
        return JSON.stringify(normalizeComparableValue(left)) === JSON.stringify(normalizeComparableValue(right));
    }

    function isIgnoredChangedColumn(columnName) {
        return IGNORED_CHANGED_COLUMNS.has(String(columnName || '').trim().toLowerCase());
    }

    function getOperation(extraData) {
        return typeof extraData?.operation === 'string'
            ? extraData.operation.toUpperCase()
            : '';
    }

    function getSourceTable(extraData, entry) {
        return String(extraData?.source_table || extraData?.source_table_name || entry?.targetType || '').trim();
    }

    function getRecordRows(extraData) {
        const operation = getOperation(extraData);
        const semanticContext = isRecordObject(extraData?.semantic_context)
            ? extraData.semantic_context
            : null;
        const withSemanticContext = (record) => {
            if (!record || !semanticContext) {
                return record;
            }

            return {
                ...semanticContext,
                ...record
            };
        };
        const currentRecord = isRecordObject(extraData?.new_row_data)
            ? withSemanticContext(extraData.new_row_data)
            : (isRecordObject(extraData?.row_data) ? withSemanticContext(extraData.row_data) : null);
        const previousRecord = isRecordObject(extraData?.old_row_data)
            ? withSemanticContext(extraData.old_row_data)
            : (isRecordObject(extraData?.previous_row_data) ? withSemanticContext(extraData.previous_row_data) : null);
        const deletedRecord = isRecordObject(extraData?.old_row_data)
            ? withSemanticContext(extraData.old_row_data)
            : (isRecordObject(extraData?.row_data) ? withSemanticContext(extraData.row_data) : null);

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

    function isHiddenRecordColumn(columnName) {
        const normalizedColumnName = normalizeSourceTable(columnName);
        if (!normalizedColumnName || IGNORED_CHANGED_COLUMNS.has(normalizedColumnName)) {
            return true;
        }

        if (SENSITIVE_RECORD_COLUMNS.has(normalizedColumnName)) {
            return true;
        }

        if (normalizedColumnName.includes('password')
            || normalizedColumnName.includes('signature')
            || normalizedColumnName.includes('base64')) {
            return true;
        }

        return normalizedColumnName.endsWith('_data_url') && normalizedColumnName !== 'background_image_data_url';
    }

    function addColumn(columns, columnSet, availableColumns, columnName) {
        const normalizedColumnName = normalizeSourceTable(columnName);
        if (!normalizedColumnName || columnSet.has(normalizedColumnName) || isHiddenRecordColumn(normalizedColumnName)) {
            return;
        }

        const actualColumnName = availableColumns.get(normalizedColumnName);
        if (!actualColumnName) {
            return;
        }

        columnSet.add(normalizedColumnName);
        columns.push(actualColumnName);
    }

    function collectAvailableColumns(recordRows) {
        const availableColumns = new Map();
        recordRows.forEach((recordRow) => {
            Object.keys(recordRow.data || {}).forEach((columnName) => {
                const normalizedColumnName = normalizeSourceTable(columnName);
                if (normalizedColumnName && !availableColumns.has(normalizedColumnName)) {
                    availableColumns.set(normalizedColumnName, columnName);
                }
            });
        });

        return availableColumns;
    }

    function collectColumns(sourceTable, recordRows, columnOrder, changedColumns) {
        const columns = [];
        const columnSet = new Set();
        const availableColumns = collectAvailableColumns(recordRows);
        const normalizedSourceTable = normalizeSourceTable(sourceTable);
        const baseColumns = BASE_RECORD_COLUMNS[normalizedSourceTable] || [];

        baseColumns.forEach((columnName) => addColumn(columns, columnSet, availableColumns, columnName));
        changedColumns.forEach((columnName) => addColumn(columns, columnSet, availableColumns, columnName));

        if (columns.length === 0) {
            normalizeColumnList(columnOrder).forEach((columnName) => addColumn(columns, columnSet, availableColumns, columnName));
        }

        if (columns.length === 0) {
            Array.from(availableColumns.values()).forEach((columnName) => addColumn(columns, columnSet, availableColumns, columnName));
        }

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

    function getChangedColumnSet(extraData, recordRows) {
        const changedColumns = new Set();
        const changedFields = Array.isArray(extraData?.changed_fields) ? extraData.changed_fields : [];

        changedFields.forEach((change) => {
            const fieldName = String(change?.field || '').trim();
            if (fieldName && !isIgnoredChangedColumn(fieldName)) {
                changedColumns.add(fieldName.toLowerCase());
            }
        });

        if (changedColumns.size > 0) {
            return changedColumns;
        }

        const previousRecord = recordRows.find((recordRow) => recordRow.type === 'old')?.data;
        const currentRecord = recordRows.find((recordRow) => recordRow.type === 'new')?.data;
        if (!previousRecord || !currentRecord) {
            return changedColumns;
        }

        Object.keys({ ...previousRecord, ...currentRecord }).forEach((columnName) => {
            if (isIgnoredChangedColumn(columnName)) {
                return;
            }

            if (!areValuesEqual(previousRecord[columnName], currentRecord[columnName])) {
                changedColumns.add(columnName.toLowerCase());
            }
        });

        return changedColumns;
    }

    function getRecordRowKey(recordRow) {
        return JSON.stringify({
            type: recordRow.type,
            data: normalizeRecordForKey(recordRow.data)
        });
    }

    function getRecordTableItems(extraData, entry) {
        if (Array.isArray(extraData?.items) && extraData.items.length > 0) {
            const items = extraData.items.filter(isRecordObject);
            const preferredSourceTable = normalizeSourceTable(entry?.sourceTable || extraData?.source_table);
            const preferredItem = preferredSourceTable
                ? items.find((item) => normalizeSourceTable(item?.source_table) === preferredSourceTable)
                : null;
            const semanticContext = isRecordObject(extraData?.semantic_context)
                ? extraData.semantic_context
                : null;
            const attachContext = (item) => (
                item && semanticContext
                    ? { ...item, semantic_context: semanticContext }
                    : item
            );

            return preferredItem ? [attachContext(preferredItem)] : items.slice(0, 1).map(attachContext);
        }

        return isRecordObject(extraData) ? [extraData] : [];
    }

    function buildRecordTableGroups(extraData, entry) {
        const groups = [];
        const groupMap = new Map();

        getRecordTableItems(extraData, entry).forEach((item) => {
            const recordRows = getRecordRows(item);
            if (recordRows.length === 0) {
                return;
            }

            const sourceTable = getSourceTable(item, entry);
            let group = groupMap.get(sourceTable);
            if (!group) {
                group = {
                    sourceItem: item,
                    sourceTable,
                    columnOrder: [],
                    recordRows: [],
                    recordRowKeys: new Set(),
                    changedColumns: new Set()
                };
                groupMap.set(sourceTable, group);
                groups.push(group);
            }

            mergeColumnOrder(group.columnOrder, normalizeColumnList(item.column_order));
            const itemChangedColumns = getChangedColumnSet(item, recordRows);
            itemChangedColumns.forEach((columnName) => group.changedColumns.add(columnName));

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

    function getPrimarySourceTable(extraData, entry) {
        const item = getRecordTableItems(extraData, entry)[0] || extraData;
        return getSourceTable(item, entry) || '—';
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

    function appendRecordTable(host, sourceTable, recordRow, columns, changedColumns, operation) {
        if (!recordRow || columns.length === 0) {
            return false;
        }

        const table = document.createElement('table');
        table.className = 'logs-modal__record-table';

        const thead = document.createElement('thead');
        const headerRow = document.createElement('tr');
        headerRow.className = 'table_tr';
        columns.forEach((columnName) => appendCell(headerRow, 'th', columnName));
        thead.appendChild(headerRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        const row = document.createElement('tr');
        row.className = recordRow.type === 'old'
            ? 'logs-modal__record-row logs-modal__record-row--old'
            : 'logs-modal__record-row logs-modal__record-row--new';
        columns.forEach((columnName) => {
            const cell = appendCell(row, 'td', formatRecordValue(columnName, recordRow.data?.[columnName]));
            if (changedColumns.has(normalizeSourceTable(columnName))) {
                cell.classList.add('logs-modal__changed-cell');
            }
        });
        tbody.appendChild(row);
        table.appendChild(tbody);
        host.appendChild(table);

        return true;
    }

    function appendRecordTables(host, group) {
        const operation = getOperation(group.sourceItem);
        const columns = collectColumns(group.sourceTable, group.recordRows, group.columnOrder, group.changedColumns);
        return group.recordRows.reduce((count, recordRow) => (
            appendRecordTable(host, group.sourceTable, recordRow, columns, group.changedColumns, operation)
                ? count + 1
                : count
        ), 0);
    }

    function renderRecordTable(modal, extraData, entry) {
        const host = modal.querySelector('[data-role="log-record-table"]');
        if (!host) {
            return;
        }

        host.replaceChildren();

        const renderedCount = buildRecordTableGroups(extraData, entry)
            .reduce((count, group) => {
                return count + appendRecordTables(host, group);
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
        if (!content) {
            return;
        }

        content.style.removeProperty('--logs-modal-width');
    }

    async function openLogEntryModal(logId, sourceTable) {
        let entry;
        try {
            entry = await loadLogEntryDetails(logId, sourceTable);
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
        setText(modal, 'log-table-name', getPrimarySourceTable(extraData, entry));

        renderRecordTable(modal, extraData, entry);
        hideRowTooltip();

        if (typeof window.showSiteModal === 'function') {
            window.showSiteModal(modal);
            window.requestAnimationFrame(() => resizeLogModal(modal));
        }
    }

    window.openLogEntryModalByTrigger = function openLogEntryModalByTrigger(element) {
        const logId = Number(element?.dataset?.logId || 0);
        openLogEntryModal(logId, element?.dataset?.logSourceTable || '');
    };

    window.openLogEntryModalByRow = function openLogEntryModalByRow(element) {
        const logId = Number(element?.dataset?.logId || 0);
        openLogEntryModal(logId, element?.dataset?.logSourceTable || '');
    };

    function ensureRowTooltip() {
        if (rowTooltip) {
            return rowTooltip;
        }

        rowTooltip = document.createElement('div');
        rowTooltip.className = 'logs-page__cursor-tooltip';
        rowTooltip.textContent = 'Смотреть';
        rowTooltip.setAttribute('aria-hidden', 'true');
        document.body.appendChild(rowTooltip);
        return rowTooltip;
    }

    function applyRowTooltipPosition() {
        rowTooltipFrame = 0;
        if (!activeTooltipRow || !rowTooltip) {
            return;
        }

        rowTooltip.style.transform = `translate3d(${latestTooltipX + ROW_TOOLTIP_OFFSET_X}px, ${latestTooltipY + ROW_TOOLTIP_OFFSET_Y}px, 0)`;
    }

    function queueRowTooltipPosition(event) {
        if (activeTooltipRow && !activeTooltipRow.isConnected) {
            hideRowTooltip();
            return;
        }

        latestTooltipX = event.clientX;
        latestTooltipY = event.clientY;
        if (!rowTooltipFrame) {
            rowTooltipFrame = window.requestAnimationFrame(applyRowTooltipPosition);
        }
    }

    function showRowTooltip(row, event) {
        activeTooltipRow = row;
        ensureRowTooltip().classList.add('is-visible');
        queueRowTooltipPosition(event);
    }

    function hideRowTooltip() {
        activeTooltipRow = null;
        if (rowTooltipFrame) {
            window.cancelAnimationFrame(rowTooltipFrame);
            rowTooltipFrame = 0;
        }

        if (rowTooltip) {
            rowTooltip.classList.remove('is-visible');
            rowTooltip.style.transform = 'translate3d(-9999px, -9999px, 0)';
        }
    }

    document.addEventListener('click', (event) => {
        const row = event.target.closest('.logs-table tbody tr[data-log-id]');
        if (!row || event.target.closest('a, button, input, select, textarea')) {
            return;
        }

        openLogEntryModal(Number(row.dataset.logId || 0), row.dataset.logSourceTable || '');
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Enter') {
            return;
        }

        const row = event.target.closest('.logs-table tbody tr[data-log-id]');
        if (!row) {
            return;
        }

        openLogEntryModal(Number(row.dataset.logId || 0), row.dataset.logSourceTable || '');
    });

    document.addEventListener('mouseover', (event) => {
        const row = event.target.closest('.logs-table tbody tr[data-log-id]');
        if (!row || activeTooltipRow === row) {
            return;
        }

        showRowTooltip(row, event);
    });

    document.addEventListener('mousemove', (event) => {
        if (!activeTooltipRow) {
            return;
        }

        queueRowTooltipPosition(event);
    });

    document.addEventListener('mouseout', (event) => {
        if (!activeTooltipRow || activeTooltipRow.contains(event.relatedTarget)) {
            return;
        }

        hideRowTooltip();
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
