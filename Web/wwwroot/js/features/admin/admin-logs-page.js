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

    function renderChangedFields(modal, extraData) {
        const section = modal.querySelector('[data-role="log-changes-section"]');
        const host = modal.querySelector('[data-role="log-changes"]');
        if (!section || !host) {
            return;
        }

        host.replaceChildren();
        const changedFields = Array.isArray(extraData?.changed_fields)
            ? extraData.changed_fields
            : [];

        if (changedFields.length === 0) {
            section.classList.add('u-hidden');
            return;
        }

        changedFields.forEach((change) => {
            const row = document.createElement('div');
            row.className = 'logs-modal__change-item';
            row.textContent = `${formatValue(change?.field)}: ${formatValue(change?.new_value)} (старое значение: ${formatValue(change?.old_value)})`;
            host.appendChild(row);
        });

        section.classList.remove('u-hidden');
    }

    function renderChangeReason(modal, extraData) {
        const section = modal.querySelector('[data-role="log-reason-section"]');
        const valueNode = modal.querySelector('[data-role="log-reason"]');
        if (!section || !valueNode) {
            return;
        }

        const changeReason = typeof extraData?.change_reason === 'string'
            ? extraData.change_reason.trim()
            : '';

        if (!changeReason) {
            valueNode.textContent = '';
            section.classList.add('u-hidden');
            return;
        }

        valueNode.textContent = changeReason;
        section.classList.remove('u-hidden');
    }

    function openLogEntryModal(logId) {
        const entry = getLogEntry(logId);
        const modal = document.getElementById('logEntryModal');
        if (!entry || !modal) {
            return;
        }

        const extraData = parseExtraData(entry);
        const objectLabel = entry.targetName && entry.targetType && entry.targetName !== entry.targetType
            ? `${entry.targetName} (${entry.targetType})`
            : (entry.targetName || entry.targetType || '—');
        const rawJson = typeof entry.extraDataJson === 'string' && entry.extraDataJson.trim().length > 0
            ? entry.extraDataJson.trim()
            : '—';

        setText(modal, 'log-modal-subtitle', `Запись №${entry.id}`);
        setText(modal, 'log-date', entry.date);
        setText(modal, 'log-user', entry.user);
        setText(modal, 'log-event', entry.eventType);
        setText(modal, 'log-object', objectLabel);
        setText(modal, 'log-table', extraData?.source_table_name || entry.targetType);
        setText(modal, 'log-record-id', extraData?.target_id);
        setText(modal, 'log-description', entry.description);
        setText(modal, 'log-json', rawJson);

        renderChangedFields(modal, extraData);
        renderChangeReason(modal, extraData);

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
})();
