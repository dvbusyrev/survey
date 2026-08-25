(function () {
    window.__surveyAdminDateFilterController?.destroy?.();

    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const DATE_FILTER_SELECTOR = '[data-role="survey-date-filter"]';
    const ORGANIZATION_FILTER_SELECTOR = '[data-role="survey-organization-filter"]';
    const SURVEY_NAME_FILTER_SELECTOR = '[data-role="survey-name-filter"]';

    const { isIsoWithin } = window.SurveyFilterCore;
    const serverFilters = window.SurveyServerFilterState;
    const dateFilter = window.SurveyDateFilter;
    const checkboxFilter = window.SurveyCheckboxFilter;
    const filterSummary = window.SurveyFilterSummary;
    const filterPopover = window.SurveyFilterPopover;
    const rowFiltering = window.SurveyRowFiltering;

    const mountedControllers = new Set();
    const mountedControllerByPage = new WeakMap();

    const checkboxDefinitions = {
        organization: {
            selector: ORGANIZATION_FILTER_SELECTOR,
            triggerRole: 'survey-organization-filter-trigger',
            labelRole: 'survey-organization-filter-label',
            popoverRole: 'survey-organization-filter-popover',
            optionsRole: 'survey-organization-filter-options',
            summaryRole: 'survey-organization-filter-summary',
            clearRole: 'survey-organization-filter-clear',
            inlineClearRole: 'survey-organization-filter-inline-clear',
            closeRole: 'survey-organization-filter-close',
            createState(page) {
                const config = serverFilters.getConfig(page);
                return {
                    isOpen: false,
                    serverMode: serverFilters.isServerPage(page),
                    availableOrganizations: rowFiltering.collectAvailableOrganizations(page),
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
            selector: SURVEY_NAME_FILTER_SELECTOR,
            triggerRole: 'survey-name-filter-trigger',
            labelRole: 'survey-name-filter-label',
            popoverRole: 'survey-name-filter-popover',
            optionsRole: 'survey-name-filter-options',
            summaryRole: 'survey-name-filter-summary',
            clearRole: 'survey-name-filter-clear',
            inlineClearRole: 'survey-name-filter-inline-clear',
            closeRole: 'survey-name-filter-close',
            createState(page) {
                const config = serverFilters.getConfig(page);
                return {
                    isOpen: false,
                    serverMode: serverFilters.isServerPage(page),
                    availableSurveyNames: rowFiltering.collectAvailableSurveyNames(page),
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

    function getPages(root) {
        if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
            return Array.from(document.querySelectorAll(PAGE_SELECTOR));
        }

        if (!(root instanceof Element)) {
            return [];
        }

        return Array.from(new Set([
            root.matches(PAGE_SELECTOR) ? root : null,
            root.closest(PAGE_SELECTOR),
            ...root.querySelectorAll(PAGE_SELECTOR)
        ].filter(Boolean)));
    }

    function getAllInstances() {
        return Array.from(mountedControllers).flatMap((controller) => controller.instances);
    }

    function closeAllPopovers(exceptRoot = null) {
        getAllInstances().forEach((instance) => {
            if (instance.root !== exceptRoot) {
                filterPopover.setOpen(instance, false);
            }
        });
    }

    function setPopoverOpen(instance, isOpen) {
        filterPopover.setOpen(instance, isOpen);
    }

    function updateSummaries(controller, visibleCount, totalCount) {
        if (controller.date) {
            filterSummary.updateDate(controller.date, visibleCount, totalCount);
        }

        if (controller.organization) {
            checkboxDefinitions.organization.updateSummary(controller.organization, visibleCount, totalCount);
        }

        if (controller.survey) {
            checkboxDefinitions.survey.updateSummary(controller.survey, visibleCount, totalCount);
        }
    }

    function applyPageFilters(page) {
        const controller = mountedControllerByPage.get(page);
        if (!controller) {
            return;
        }

        const rows = rowFiltering.getRows(page);
        if (serverFilters.isServerPage(page)) {
            const totalCount = Number.parseInt(String(page?.dataset?.totalCount || rows.length), 10) || rows.length;
            updateSummaries(controller, rows.length, totalCount);
            rowFiltering.syncEmptyRow(page, rows, rows.length);
            return;
        }

        const bounds = controller.date
            ? dateFilter.getActiveFilterBounds(controller.date.state)
            : null;
        const result = rowFiltering.applyLocalFilters(page, {
            dateBounds: bounds,
            selectedOrganizations: controller.organization?.state?.selectedOrganizations || [],
            selectedSurveyNames: controller.survey?.state?.selectedSurveyNames || [],
            isIsoWithin
        });

        updateSummaries(controller, result.visibleCount, result.totalCount);
    }

    function createDateInstance(page) {
        const root = page.querySelector(DATE_FILTER_SELECTOR);
        if (!root) {
            return null;
        }

        const instance = dateFilter.createInstance(root, {
            pageSelector: PAGE_SELECTOR,
            serverFilters,
            filterPopover,
            closeAllPopovers,
            setPopoverOpen,
            applyFilter: (filterInstance) => applyPageFilters(filterInstance.page)
        });

        if (instance && serverFilters.consumePendingOpenFilter(instance.page, 'date')) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }

        return instance;
    }

    function createCheckboxInstance(page, type) {
        const definition = checkboxDefinitions[type];
        const root = definition ? page.querySelector(definition.selector) : null;
        if (!definition || !root) {
            return null;
        }

        const instance = checkboxFilter.createInstance(root, {
            ...definition,
            pendingFilterName: type
        }, {
            pageSelector: PAGE_SELECTOR,
            serverFilters,
            filterPopover,
            closeAllPopovers,
            setPopoverOpen,
            applyPageFilters
        });

        if (instance && serverFilters.consumePendingOpenFilter(instance.page, type)) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }

        return instance;
    }

    function mountPage(page) {
        if (!(page instanceof Element) || !page.matches(PAGE_SELECTOR)) {
            return null;
        }

        const mountedController = mountedControllerByPage.get(page);
        if (mountedController) {
            return mountedController;
        }

        let disposed = false;
        const controller = {
            page,
            date: null,
            organization: null,
            survey: null,
            instances: [],
            destroy() {
                if (disposed) {
                    return;
                }

                disposed = true;
                page.removeEventListener('page:unmount', controller.destroy);
                controller.instances.forEach((instance) => instance.destroy?.());
                controller.instances = [];
                mountedControllerByPage.delete(page);
                mountedControllers.delete(controller);
            }
        };

        mountedControllerByPage.set(page, controller);
        mountedControllers.add(controller);

        controller.date = createDateInstance(page);
        controller.organization = createCheckboxInstance(page, 'organization');
        controller.survey = createCheckboxInstance(page, 'survey');
        controller.instances = [controller.date, controller.organization, controller.survey].filter(Boolean);

        if (controller.instances.length === 0) {
            controller.destroy();
            return null;
        }

        page.addEventListener('page:unmount', controller.destroy);
        applyPageFilters(page);
        return controller;
    }

    function createControllerGroup(controllers) {
        let disposed = false;
        return {
            destroy() {
                if (disposed) {
                    return;
                }

                disposed = true;
                controllers.slice().reverse().forEach((controller) => controller?.destroy?.());
            }
        };
    }

    function mount(root = document) {
        return createControllerGroup(getPages(root).map(mountPage).filter(Boolean));
    }

    function destroy(root = document) {
        const pages = getPages(root);
        if (root === document || root?.nodeType === Node.DOCUMENT_NODE) {
            Array.from(mountedControllers).forEach((controller) => controller.destroy());
            return;
        }

        Array.from(mountedControllers).forEach((controller) => {
            if (pages.includes(controller.page)) {
                controller.destroy();
            }
        });
    }

    window.SurveyFilters = { mount, destroy };
    window.__surveyAdminDateFilterController = {
        destroy: () => destroy(document)
    };
})();
