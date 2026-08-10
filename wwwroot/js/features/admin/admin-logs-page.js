(function () {
    const LOG_ROW_SELECTOR = '.logs-table tbody tr[data-log-id]';
    const INTERACTIVE_SELECTOR = 'a, button, input, select, textarea';
    const logsData = window.AdminLogsData;
    const logsRecords = window.AdminLogsRecords;
    const logsDescriptionSort = window.AdminLogsDescriptionSort;

    if (!logsData || !logsRecords || !logsDescriptionSort) {
        console.error('Модули страницы журнала событий не загружены.');
        return;
    }

    const rowTooltip = window.AppUi.createRowTooltip();
    let activePageRoot = null;
    let activeMountToken = 0;

    const entryStore = logsData.createLogEntryStore(() => activePageRoot);
    const descriptionSort = logsDescriptionSort.createDescriptionSortSync(() => activePageRoot);

    function setText(container, role, value) {
        const target = container.querySelector(`[data-role="${role}"]`);
        if (target) {
            target.textContent = value && String(value).trim().length > 0
                ? String(value).trim()
                : '—';
        }
    }

    function getLogModal() {
        return activePageRoot?.querySelector('#logEntryModal') || document.getElementById('logEntryModal');
    }

    async function openLogEntryModal(logId, sourceTable) {
        const mountToken = activeMountToken;
        let entry;
        try {
            entry = await entryStore.loadDetails(logId, sourceTable);
        } catch (error) {
            const message = error instanceof Error ? error.message : 'Не удалось загрузить событие.';
            console.error('Не удалось загрузить событие журнала:', error);
            window.AppUi?.notify?.(message, 'error');
            return;
        }

        if (mountToken !== activeMountToken || !activePageRoot?.isConnected) {
            return;
        }

        const modal = getLogModal();
        if (!entry || !modal) {
            return;
        }

        const extraData = logsRecords.parseExtraData(entry);

        setText(modal, 'log-modal-subtitle', logsRecords.getEventTitle(entry, extraData));
        setText(modal, 'log-date', entry.date);
        setText(modal, 'log-user', entry.user);
        setText(modal, 'log-event', entry.eventType);
        setText(modal, 'log-table-name', logsRecords.getPrimarySourceTable(extraData, entry));

        logsRecords.renderRecordTable(modal, extraData, entry);
        rowTooltip.hide();

        if (typeof window.showSiteModal === 'function') {
            window.showSiteModal(modal);
        }
    }

    function isActiveLogRow(row) {
        return Boolean(row && activePageRoot?.contains(row));
    }

    function getLogRowFromEvent(event) {
        const row = event.target.closest(LOG_ROW_SELECTOR);
        return isActiveLogRow(row) ? row : null;
    }

    function openLogRow(row) {
        if (!row) {
            return;
        }

        openLogEntryModal(Number(row.dataset.logId || 0), row.dataset.logSourceTable || '');
    }

    function handlePageClick(event) {
        if (event.target.closest(INTERACTIVE_SELECTOR)) {
            return;
        }

        openLogRow(getLogRowFromEvent(event));
    }

    function handlePageKeydown(event) {
        if (event.key !== 'Enter') {
            return;
        }

        openLogRow(getLogRowFromEvent(event));
    }

    function handlePageMouseOver(event) {
        const row = getLogRowFromEvent(event);
        if (!row || rowTooltip.isActiveRow(row)) {
            return;
        }

        rowTooltip.show(row, event);
    }

    function handlePageMouseMove(event) {
        if (!rowTooltip.hasActiveRow()) {
            return;
        }

        rowTooltip.move(event);
    }

    function handlePageMouseOut(event) {
        if (!rowTooltip.hasActiveRow() || rowTooltip.activeRowContains(event.relatedTarget)) {
            return;
        }

        rowTooltip.hide();
    }

    function mountLogsPage(page, scope) {
        activePageRoot = page;
        activeMountToken += 1;
        entryStore.reset();

        scope.listen(page, 'click', handlePageClick);
        scope.listen(page, 'keydown', handlePageKeydown);
        scope.listen(page, 'mouseover', handlePageMouseOver);
        scope.listen(page, 'mousemove', handlePageMouseMove);
        scope.listen(page, 'mouseout', handlePageMouseOut);
        scope.listen(window, 'resize', descriptionSort.sync);
        scope.listen(window, 'load', descriptionSort.sync, { once: true });

        if (document.fonts?.ready) {
            const mountToken = activeMountToken;
            document.fonts.ready
                .then(() => {
                    if (mountToken === activeMountToken) {
                        descriptionSort.sync();
                    }
                })
                .catch(() => {});
        }

        descriptionSort.sync();
        return () => {
            activeMountToken += 1;
            descriptionSort.cancel();
            rowTooltip.destroy();
            entryStore.reset();
            if (activePageRoot === page) {
                activePageRoot = null;
            }
        };
    }

    if (window.AppPageLifecycle?.register) {
        window.AppPageLifecycle.register(
            'admin-logs-page',
            '.app-page[data-page="get_logs"]',
            mountLogsPage
        );
        return;
    }

    const fallbackPage = document.querySelector('.app-page[data-page="get_logs"]');
    if (fallbackPage) {
        const fallbackScope = window.AppPageLifecycle?.createScope?.() || {
            listen(target, type, handler, options) {
                target.addEventListener(type, handler, options);
            }
        };
        mountLogsPage(fallbackPage, fallbackScope);
    }
})();
