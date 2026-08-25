(function () {
    const normalizeSourceTable = window.AdminLogsData?.normalizeSourceTable || ((value) => String(value || '').trim().toLowerCase());
    const SENSITIVE_RECORD_COLUMNS = new Set([
        'password',
        'hash_password',
        'csp',
        'signature',
        'signed_content_base64',
        'recipient_emails',
        'smtp_password'
    ]);
    const HIDDEN_IMAGE_COLUMNS = new Set([
        'background_image_content_type',
        'background_image_data_url',
        'background_image_file_name'
    ]);
    const IMAGE_COLUMNS = new Set(['background_image', ...HIDDEN_IMAGE_COLUMNS]);
    const HIDDEN_RECORD_COLUMN_MARKERS = ['password', 'signature', 'base64'];
    const BASE_RECORD_COLUMNS = {
        app_user: ['id_user', 'full_name'],
        organization: ['id_organization', 'organization_name'],
        survey: ['id_survey', 'name_survey'],
        survey_question: ['id_question', 'id_survey', 'question_order', 'question_text'],
        organization_survey: ['id_organization_survey', 'id_survey', 'id_organization'],
        answer: ['id_survey', 'name_survey', 'completed_by', 'id_organization', 'organization_name', 'id_answer', 'completion_date'],
        answer_item: ['id_survey', 'name_survey', 'completed_by', 'id_organization', 'organization_name', 'id_answer', 'question_order', 'question_text', 'rating', 'comment'],
        auto_creation_config: ['id_config', 'is_enabled'],
        survey_template_auto_creation_config: ['id_config', 'id_survey_template'],
        email_config: ['id_config', 'smtp_host', 'smtp_port'],
        theme_config: ['id_config', 'font_color', 'background_color']
    };

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

    function getFileName(value) {
        const normalizedValue = String(value || '').trim();
        if (!normalizedValue) {
            return '';
        }

        const pathWithoutQuery = normalizedValue.split(/[?#]/, 1)[0].replace(/\\/g, '/');
        const fileName = pathWithoutQuery.split('/').filter(Boolean).pop() || pathWithoutQuery;

        try {
            return decodeURIComponent(fileName);
        } catch (error) {
            return fileName;
        }
    }

    function formatRecordValue(columnName, value, recordData) {
        const normalizedColumnName = normalizeSourceTable(columnName);
        if (value == null) {
            return 'null';
        }

        if (normalizedColumnName === 'background_image') {
            return getFileName(recordData?.background_image_file_name) || 'Изображение';
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

    function isHiddenRecordColumn(columnName) {
        const normalizedColumnName = normalizeSourceTable(columnName);
        if (!normalizedColumnName) {
            return true;
        }

        if (SENSITIVE_RECORD_COLUMNS.has(normalizedColumnName)) {
            return true;
        }

        if (HIDDEN_IMAGE_COLUMNS.has(normalizedColumnName)) {
            return true;
        }

        if (HIDDEN_RECORD_COLUMN_MARKERS.some((marker) => normalizedColumnName.includes(marker))) {
            return true;
        }

        return normalizedColumnName.endsWith('_data_url');
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

        function add(columnName) {
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

        function addMany(values) {
            Array.from(values || []).forEach(add);
        }

        addMany(baseColumns);
        if (Array.from(changedColumns).some((columnName) => HIDDEN_IMAGE_COLUMNS.has(normalizeSourceTable(columnName)))) {
            add('background_image');
        }
        addMany(changedColumns);

        if (columns.length === 0) {
            addMany(normalizeColumnList(columnOrder));
        }

        if (columns.length === 0) {
            addMany(availableColumns.values());
        }

        return columns;
    }

    function getChangedColumnSet(extraData, recordRows) {
        const changedColumns = new Set();
        const changedFields = Array.isArray(extraData?.changed_fields) ? extraData.changed_fields : [];

        changedFields.forEach((change) => {
            const fieldName = String(change?.field || '').trim();
            if (fieldName) {
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

    function dedupeRecordRows(recordRows) {
        const seenRows = new Set();
        return recordRows.filter((recordRow) => {
            const recordRowKey = getRecordRowKey(recordRow);
            if (seenRows.has(recordRowKey)) {
                return false;
            }

            seenRows.add(recordRowKey);
            return true;
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
        return getRecordTableItems(extraData, entry)
            .map((item) => {
                const recordRows = dedupeRecordRows(getRecordRows(item));
                if (recordRows.length === 0) {
                    return null;
                }

                return {
                    sourceTable: getSourceTable(item, entry),
                    columnOrder: normalizeColumnList(item.column_order),
                    recordRows,
                    changedColumns: getChangedColumnSet(item, recordRows)
                };
            })
            .filter(Boolean);
    }

    function getPrimarySourceTable(extraData, entry) {
        const item = getRecordTableItems(extraData, entry)[0] || extraData;
        return getSourceTable(item, entry) || '—';
    }

    function renderEmptyRecordTable(host) {
        host.appendChild(window.AppUi.createElement('p', {
            className: 'logs-modal__empty',
            text: 'Данные записи не найдены.'
        }));
    }

    function getRecordRowClass(recordRow) {
        return recordRow.type === 'old'
            ? 'logs-modal__record-row logs-modal__record-row--old'
            : 'logs-modal__record-row logs-modal__record-row--new';
    }

    function buildRecordCells(recordRow, columns, changedColumns) {
        return columns.map((columnName) => {
            const normalizedColumnName = normalizeSourceTable(columnName);
            const isChangedImage = normalizedColumnName === 'background_image'
                && Array.from(changedColumns).some((changedColumn) => (
                    IMAGE_COLUMNS.has(normalizeSourceTable(changedColumn))
                ));

            return {
                text: formatRecordValue(columnName, recordRow.data?.[columnName], recordRow.data),
                className: changedColumns.has(normalizedColumnName) || isChangedImage
                    ? 'logs-modal__changed-cell'
                    : ''
            };
        });
    }

    function appendRecordTable(host, recordRow, columns, changedColumns) {
        if (!recordRow || columns.length === 0) {
            return false;
        }

        const tableParts = window.AppUi.createTable({
            className: 'app-modal-table logs-modal__record-table'
        });
        columns.forEach((columnName) => tableParts.appendHeaderCell(columnName));

        tableParts.appendRow(buildRecordCells(recordRow, columns, changedColumns), {
            className: getRecordRowClass(recordRow)
        });
        host.appendChild(tableParts.table);

        return true;
    }

    function appendRecordTables(host, group) {
        const columns = collectColumns(group.sourceTable, group.recordRows, group.columnOrder, group.changedColumns);
        return group.recordRows.reduce((count, recordRow) => (
            appendRecordTable(host, recordRow, columns, group.changedColumns)
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
            .reduce((count, group) => count + appendRecordTables(host, group), 0);

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

    window.AdminLogsRecords = {
        parseExtraData,
        renderRecordTable,
        getEventTitle,
        getPrimarySourceTable
    };
})();
