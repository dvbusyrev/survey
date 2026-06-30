(function () {
    const existingController = window.__surveyAdminDateFilterController;
    if (existingController && typeof existingController.destroy === 'function') {
        existingController.destroy();
    }

    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const DATE_FILTER_SELECTOR = '[data-role="survey-date-filter"]';
    const ORGANIZATION_FILTER_SELECTOR = '[data-role="survey-organization-filter"]';
    const SURVEY_NAME_FILTER_SELECTOR = '[data-role="survey-name-filter"]';
    const SURVEY_ROW_SELECTOR = 'tr[data-survey-date-begin][data-survey-date-end]';

    const { parseIso, isIsoWithin } = window.SurveyFilterCore;
    const serverFilters = window.SurveyServerFilterState;
    const dateFilter = window.SurveyDateFilter;
    const checkboxFilter = window.SurveyCheckboxFilter;
    const filterSummary = window.SurveyFilterSummary;
    const filterPopover = window.SurveyFilterPopover;

    const dateInstances = new Map();
    const organizationInstances = new Map();
    const surveyNameInstances = new Map();
    const allInstanceCollections = [dateInstances, organizationInstances, surveyNameInstances];
    const mountedPageControllers = new Set();
    const mountedPageControllerByPage = new WeakMap();
    let documentListenerMountCount = 0;

    const CHECKBOX_FILTER_DEFINITIONS = {
        organization: {
            instances: organizationInstances,
            selector: ORGANIZATION_FILTER_SELECTOR,
            triggerRole: 'survey-organization-filter-trigger',
            labelRole: 'survey-organization-filter-label',
            popoverRole: 'survey-organization-filter-popover',
            optionsRole: 'survey-organization-filter-options',
            summaryRole: 'survey-organization-filter-summary',
            clearRole: 'survey-organization-filter-clear',
            closeRole: 'survey-organization-filter-close',
            pendingFilterName: 'organization',
            createState(page) {
                const config = serverFilters.getConfig(page);
                return {
                    isOpen: false,
                    serverMode: serverFilters.isServerPage(page),
                    availableOrganizations: collectAvailableOrganizations(page),
                    availableOrganizationOptions: config?.organizationOptions || [],
                    selectedOrganizations: [],
                    selectedOrganizationIds: [...(config?.selectedOrganizationIds || [])]
                };
            },
            updateSummary(instance, visibleCount, totalCount) {
                filterSummary.updateOrganization(instance, visibleCount, totalCount, serverFilters);
            }
        },
        survey: {
            instances: surveyNameInstances,
            selector: SURVEY_NAME_FILTER_SELECTOR,
            triggerRole: 'survey-name-filter-trigger',
            labelRole: 'survey-name-filter-label',
            popoverRole: 'survey-name-filter-popover',
            optionsRole: 'survey-name-filter-options',
            summaryRole: 'survey-name-filter-summary',
            clearRole: 'survey-name-filter-clear',
            closeRole: 'survey-name-filter-close',
            pendingFilterName: 'survey',
            createState(page) {
                const config = serverFilters.getConfig(page);
                return {
                    isOpen: false,
                    serverMode: serverFilters.isServerPage(page),
                    availableSurveyNames: collectAvailableSurveyNames(page),
                    availableSurveyOptions: config?.surveyOptions || [],
                    selectedSurveyNames: [],
                    selectedSurveyIds: [...(config?.selectedSurveyIds || [])]
                };
            },
            updateSummary(instance, visibleCount, totalCount) {
                filterSummary.updateSurveyName(instance, visibleCount, totalCount, serverFilters);
            }
        }
    };

    function cleanupDetachedInstances() {
        filterPopover.cleanupDetachedInstances(allInstanceCollections);
    }

    function closeAllPopovers(exceptRoot = null) {
        filterPopover.closeAll(allInstanceCollections, exceptRoot);
    }

    function setPopoverOpen(instance, isOpen) {
        filterPopover.setOpen(instance, isOpen);
    }

    function installDocumentListeners() {
        documentListenerMountCount += 1;
        if (documentListenerMountCount !== 1) {
            return;
        }

        document.addEventListener('click', handleDocumentClick);
        document.addEventListener('keydown', handleDocumentKeydown);
    }

    function removeDocumentListeners() {
        if (documentListenerMountCount <= 0) {
            documentListenerMountCount = 0;
            return;
        }

        documentListenerMountCount -= 1;
        if (documentListenerMountCount !== 0) {
            return;
        }

        document.removeEventListener('click', handleDocumentClick);
        document.removeEventListener('keydown', handleDocumentKeydown);
    }

    function getPagesFromNode(node) {
        if (node === document || node?.nodeType === Node.DOCUMENT_NODE) {
            return Array.from(document.querySelectorAll(PAGE_SELECTOR));
        }

        if (!(node instanceof Element)) {
            return [];
        }

        const pages = [];
        const ownerPage = node.closest(PAGE_SELECTOR);
        if (ownerPage) {
            pages.push(ownerPage);
        }

        if (node.matches(PAGE_SELECTOR)) {
            pages.push(node);
        }

        node.querySelectorAll(PAGE_SELECTOR).forEach((page) => {
            pages.push(page);
        });

        return Array.from(new Set(pages));
    }

    function getDataRowsFromPage(page) {
        return Array.from(page?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
    }

    function getInstanceForPage(collection, page) {
        return Array.from(collection.values()).find((instance) => instance.page === page) || null;
    }

    function parseRowOrganizations(row) {
        const rawValue = row?.dataset?.surveyOrganizations || '[]';
        try {
            const parsed = JSON.parse(rawValue);
            return Array.isArray(parsed)
                ? parsed.map((name) => String(name || '').trim()).filter(Boolean)
                : [];
        } catch (error) {
            return [];
        }
    }

    function collectAvailableOrganizations(page) {
        return Array.from(new Set(
            getDataRowsFromPage(page)
                .flatMap((row) => parseRowOrganizations(row))
                .filter(Boolean)
        )).sort((left, right) => left.localeCompare(right, 'ru'));
    }

    function getRowSurveyName(row) {
        return String(row?.dataset?.surveyName || '').trim();
    }

    function collectAvailableSurveyNames(page) {
        return Array.from(new Set(
            getDataRowsFromPage(page)
                .map((row) => getRowSurveyName(row))
                .filter(Boolean)
        )).sort((left, right) => left.localeCompare(right, 'ru'));
    }

    function getCombinedVisibleCount(rows) {
        return rows.filter((row) => (
            !row.classList.contains('is-hidden-by-date')
            && !row.classList.contains('is-hidden-by-organization')
            && !row.classList.contains('is-hidden-by-survey-name')
        )).length;
    }

    function syncEmptyRow(page, rows, visibleCount) {
        const emptyRow = page?.querySelector('[data-role="survey-filter-empty-row"]');
        if (emptyRow) {
            emptyRow.classList.toggle('is-hidden', rows.length === 0 || visibleCount > 0);
        }
    }

    function updatePageSummaries(page, visibleCount, totalCount) {
        const dateInstance = getInstanceForPage(dateInstances, page);
        const organizationInstance = getInstanceForPage(organizationInstances, page);
        const surveyNameInstance = getInstanceForPage(surveyNameInstances, page);

        if (dateInstance) {
            filterSummary.updateDate(dateInstance, visibleCount, totalCount);
        }

        if (organizationInstance) {
            CHECKBOX_FILTER_DEFINITIONS.organization.updateSummary(organizationInstance, visibleCount, totalCount);
        }

        if (surveyNameInstance) {
            CHECKBOX_FILTER_DEFINITIONS.survey.updateSummary(surveyNameInstance, visibleCount, totalCount);
        }
    }

    function applyPageFilters(page) {
        const rows = getDataRowsFromPage(page);
        if (serverFilters.isServerPage(page)) {
            updatePageSummaries(
                page,
                rows.length,
                Number.parseInt(String(page?.dataset?.totalCount || rows.length), 10) || rows.length
            );
            syncEmptyRow(page, rows, rows.length);
            return;
        }

        const totalCount = rows.length;
        const dateInstance = getInstanceForPage(dateInstances, page);
        const organizationInstance = getInstanceForPage(organizationInstances, page);
        const surveyNameInstance = getInstanceForPage(surveyNameInstances, page);
        const bounds = dateInstance ? dateFilter.getActiveFilterBounds(dateInstance.state) : null;
        const selectedOrganizations = organizationInstance?.state?.selectedOrganizations || [];
        const selectedSurveyNames = surveyNameInstance?.state?.selectedSurveyNames || [];

        rows.forEach((row) => {
            const beginIso = row.dataset.surveyDateBegin || '';
            const endIso = row.dataset.surveyDateEnd || '';
            const matchesDate = !bounds
                || (isIsoWithin(beginIso, bounds.start, bounds.end) && isIsoWithin(endIso, bounds.start, bounds.end));
            const rowOrganizations = parseRowOrganizations(row);
            const matchesOrganizations = selectedOrganizations.length === 0
                || rowOrganizations.some((name) => selectedOrganizations.includes(name));
            const rowSurveyName = getRowSurveyName(row);
            const matchesSurveyName = selectedSurveyNames.length === 0
                || selectedSurveyNames.includes(rowSurveyName);

            row.classList.remove('is-hidden');
            row.classList.toggle('is-hidden-by-date', !matchesDate);
            row.classList.toggle('is-hidden-by-organization', !matchesOrganizations);
            row.classList.toggle('is-hidden-by-survey-name', !matchesSurveyName);
        });

        const visibleCount = getCombinedVisibleCount(rows);
        syncEmptyRow(page, rows, visibleCount);
        updatePageSummaries(page, visibleCount, totalCount);
    }

    function applyFilter(instance) {
        applyPageFilters(instance.page);
    }

    function bindDateInstance(root) {
        if (!(root instanceof Element) || dateInstances.has(root)) {
            return;
        }

        dateFilter.ensurePopoverHeader(root);

        const page = root.closest(PAGE_SELECTOR);
        const tableBody = page?.querySelector('[data-role="main-table"] tbody');
        if (!page || !tableBody) {
            return;
        }

        const instance = {
            root,
            page,
            state: dateFilter.getInitialState(page, new Date(), serverFilters),
            refs: {
                trigger: root.querySelector('[data-role="survey-date-filter-trigger"]'),
                label: root.querySelector('[data-role="survey-date-filter-label"]'),
                popover: root.querySelector('[data-role="survey-date-filter-popover"]'),
                yearModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]'),
                monthModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="month"]'),
                rangeModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="range"]'),
                yearPanel: root.querySelector('[data-role="survey-date-filter-year-panel"]'),
                monthPanel: root.querySelector('[data-role="survey-date-filter-month-panel"]'),
                rangePanel: root.querySelector('[data-role="survey-date-filter-range-panel"]'),
                yearRangeLabel: root.querySelector('[data-role="survey-date-filter-year-range-label"]'),
                yearsContainer: root.querySelector('[data-role="survey-date-filter-years"]'),
                yearLabel: root.querySelector('[data-role="survey-date-filter-year-label"]'),
                monthsContainer: root.querySelector('[data-role="survey-date-filter-months"]'),
                rangeLabel: root.querySelector('[data-role="survey-date-filter-range-label"]'),
                hint: root.querySelector('[data-role="survey-date-filter-hint"]'),
                calendars: root.querySelector('[data-role="survey-date-filter-calendars"]'),
                summary: root.querySelector('[data-role="survey-date-filter-summary"]'),
                clearButton: root.querySelector('[data-role="survey-date-filter-clear"]')
            },
            handlers: {}
        };

        const callbacks = { serverFilters, applyFilter };

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const trigger = event.target.closest('[data-role="survey-date-filter-trigger"]');
            if (trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers(shouldOpen ? root : null);
                setPopoverOpen(instance, shouldOpen);
                return;
            }

            const modeButton = event.target.closest('[data-role="survey-date-filter-mode"]');
            if (modeButton && root.contains(modeButton)) {
                event.preventDefault();
                instance.state.mode = ['year', 'range'].includes(modeButton.dataset.mode)
                    ? modeButton.dataset.mode
                    : 'month';
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-range-prev"]')) {
                event.preventDefault();
                instance.state.yearViewStart -= 10;
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-range-next"]')) {
                event.preventDefault();
                instance.state.yearViewStart += 10;
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-prev"]')) {
                event.preventDefault();
                instance.state.monthViewYear -= 1;
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-next"]')) {
                event.preventDefault();
                instance.state.monthViewYear += 1;
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-range-prev"]')) {
                event.preventDefault();
                instance.state.rangeViewDate = window.SurveyFilterCore.shiftMonth(instance.state.rangeViewDate, -1);
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-range-next"]')) {
                event.preventDefault();
                instance.state.rangeViewDate = window.SurveyFilterCore.shiftMonth(instance.state.rangeViewDate, 1);
                dateFilter.render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-close"]')) {
                event.preventDefault();
                setPopoverOpen(instance, false);
                return;
            }

            const yearButton = event.target.closest('[data-role="survey-date-filter-year"]');
            if (yearButton && root.contains(yearButton)) {
                event.preventDefault();
                const selectedYear = Number.parseInt(yearButton.dataset.year || '', 10);
                if (Number.isInteger(selectedYear)) {
                    dateFilter.applyYear(instance, selectedYear, callbacks);
                }
                return;
            }

            const monthButton = event.target.closest('[data-role="survey-date-filter-month"]');
            if (monthButton && root.contains(monthButton)) {
                event.preventDefault();
                const monthIndex = Number.parseInt(monthButton.dataset.monthIndex || '', 10);
                if (Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
                    dateFilter.applyMonth(instance, monthIndex, callbacks);
                }
                return;
            }

            const dayButton = event.target.closest('[data-role="survey-date-filter-day"]');
            if (dayButton && root.contains(dayButton)) {
                event.preventDefault();
                const isoValue = dayButton.dataset.dateIso || '';
                if (parseIso(isoValue)) {
                    dateFilter.handleRangeSelection(instance, isoValue, callbacks);
                }
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-clear"]')) {
                event.preventDefault();
                dateFilter.clear(instance, callbacks);
            }
        };

        root.addEventListener('click', instance.handlers.click);

        dateInstances.set(root, instance);
        dateFilter.render(instance);
        applyFilter(instance);
        if (serverFilters.consumePendingOpenFilter(page, 'date')) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }
    }

    function bindCheckboxInstance(root, type) {
        const definition = CHECKBOX_FILTER_DEFINITIONS[type];
        const config = checkboxFilter.getConfig(type);
        if (!definition || !config || !(root instanceof Element) || definition.instances.has(root)) {
            return;
        }

        const page = root.closest(PAGE_SELECTOR);
        const tableBody = page?.querySelector('[data-role="main-table"] tbody');
        if (!page || !tableBody) {
            return;
        }

        const instance = {
            root,
            page,
            state: definition.createState(page),
            refs: {
                trigger: root.querySelector(`[data-role="${definition.triggerRole}"]`),
                label: root.querySelector(`[data-role="${definition.labelRole}"]`),
                popover: root.querySelector(`[data-role="${definition.popoverRole}"]`),
                options: root.querySelector(`[data-role="${definition.optionsRole}"]`),
                summary: root.querySelector(`[data-role="${definition.summaryRole}"]`),
                clearButton: root.querySelector(`[data-role="${definition.clearRole}"]`)
            },
            handlers: {}
        };

        const callbacks = { serverFilters, applyPageFilters };

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const trigger = event.target.closest(`[data-role="${definition.triggerRole}"]`);
            if (trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers(shouldOpen ? root : null);
                setPopoverOpen(instance, shouldOpen);
                return;
            }

            if (event.target.closest(`[data-role="${definition.closeRole}"]`)) {
                event.preventDefault();
                setPopoverOpen(instance, false);
                return;
            }

            if (event.target.closest(`[data-role="${definition.clearRole}"]`)) {
                event.preventDefault();
                checkboxFilter.clear(instance, config, callbacks);
            }
        };

        instance.handlers.change = function (event) {
            const option = event.target.closest(`[data-role="${config.optionRole}"]`);
            if (!option || !root.contains(option)) {
                return;
            }

            if (instance.state.serverMode) {
                checkboxFilter.toggleId(instance, config, option.dataset[config.idDatasetKey], Boolean(option.checked), callbacks);
                return;
            }

            checkboxFilter.toggleValue(instance, config, option.dataset[config.valueDatasetKey], Boolean(option.checked), callbacks);
        };

        root.addEventListener('click', instance.handlers.click);
        root.addEventListener('change', instance.handlers.change);

        definition.instances.set(root, instance);
        checkboxFilter.render(instance, config);
        applyPageFilters(instance.page);
        if (serverFilters.consumePendingOpenFilter(page, definition.pendingFilterName)) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }
    }

    function unbindPageInstances(page) {
        allInstanceCollections.forEach((collection) => {
            Array.from(collection.entries()).forEach(([root, instance]) => {
                if (instance.page !== page) {
                    return;
                }

                if (instance.handlers?.click) {
                    root.removeEventListener('click', instance.handlers.click);
                }
                if (instance.handlers?.change) {
                    root.removeEventListener('change', instance.handlers.change);
                }
                collection.delete(root);
            });
        });
    }

    function mountSinglePageFilters(page) {
        if (!(page instanceof Element)) {
            return null;
        }

        const existingController = mountedPageControllerByPage.get(page);
        if (existingController) {
            return existingController;
        }

        cleanupDetachedInstances();

        const dateFilterRoot = page.querySelector(DATE_FILTER_SELECTOR);
        if (dateFilterRoot) {
            bindDateInstance(dateFilterRoot);
        }

        Object.values(CHECKBOX_FILTER_DEFINITIONS).forEach((definition) => {
            const root = page.querySelector(definition.selector);
            if (root) {
                bindCheckboxInstance(root, definition.pendingFilterName);
            }
        });

        const hasMountedInstances = allInstanceCollections.some((collection) => getInstanceForPage(collection, page));
        if (!hasMountedInstances) {
            return null;
        }

        let isDestroyed = false;
        const controller = {
            page,
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                removeDocumentListeners();
                page.removeEventListener('page:unmount', controller.destroy);
                unbindPageInstances(page);
                mountedPageControllerByPage.delete(page);
                mountedPageControllers.delete(controller);
                cleanupDetachedInstances();
            }
        };

        installDocumentListeners();
        page.addEventListener('page:unmount', controller.destroy);

        mountedPageControllerByPage.set(page, controller);
        mountedPageControllers.add(controller);

        return controller;
    }

    function createCompositeController(controllers) {
        let isDestroyed = false;
        return {
            destroy() {
                if (isDestroyed) {
                    return;
                }

                isDestroyed = true;
                controllers.forEach((controller) => controller?.destroy?.());
            }
        };
    }

    function mountSurveyFilters(root = document) {
        const controllers = getPagesFromNode(root)
            .map((page) => mountSinglePageFilters(page))
            .filter(Boolean);

        return createCompositeController(controllers);
    }

    function destroySurveyFilters(root = document) {
        if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
            Array.from(mountedPageControllers).forEach((controller) => controller.destroy());
            return;
        }

        if (!(root instanceof Element)) {
            return;
        }

        Array.from(mountedPageControllers).forEach((controller) => {
            if (controller.page === root || root.contains(controller.page)) {
                controller.destroy();
            }
        });
    }

    function handleDocumentClick(event) {
        cleanupDetachedInstances();

        if (!filterPopover.containsTarget(allInstanceCollections, event.target)) {
            closeAllPopovers();
        }
    }

    function handleDocumentKeydown(event) {
        if (event.key === 'Escape') {
            closeAllPopovers();
        }
    }

    function destroy() {
        destroySurveyFilters(document);
    }

    window.SurveyFilters = {
        mount: mountSurveyFilters,
        destroy: destroySurveyFilters
    };

    window.__surveyAdminDateFilterController = {
        destroy
    };
})();
