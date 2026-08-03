(function () {
    if (window.SurveyCheckboxFilter) {
        return;
    }

    const { createElement } = window.SurveyFilterCore;

    const configs = {
        organization: {
            availableValuesKey: 'availableOrganizations',
            availableOptionsKey: 'availableOrganizationOptions',
            selectedValuesKey: 'selectedOrganizations',
            selectedIdsKey: 'selectedOrganizationIds',
            selectedConfigKey: 'selectedOrganizationIds',
            optionRole: 'survey-organization-filter-option',
            valueDatasetKey: 'organizationName',
            idDatasetKey: 'organizationId',
            emptyText: 'Организации для фильтрации не найдены.',
            filterName: 'organization'
        },
        survey: {
            availableValuesKey: 'availableSurveyNames',
            availableOptionsKey: 'availableSurveyOptions',
            selectedValuesKey: 'selectedSurveyNames',
            selectedIdsKey: 'selectedSurveyIds',
            selectedConfigKey: 'selectedSurveyIds',
            optionRole: 'survey-name-filter-option',
            valueDatasetKey: 'surveyName',
            idDatasetKey: 'surveyId',
            emptyText: 'Анкеты для фильтрации не найдены.',
            filterName: 'survey'
        }
    };

    function getConfig(type) {
        return configs[type] || null;
    }

    function getSelectedNames(instance, config, serverFilters = window.SurveyServerFilterState) {
        return instance.state.serverMode
            ? serverFilters.getSelectedOptionNames(instance.state[config.availableOptionsKey], instance.state[config.selectedIdsKey])
            : instance.state[config.selectedValuesKey];
    }

    function render(instance, config) {
        const { state, refs } = instance;
        refs.options.textContent = '';

        const hasOptions = state.serverMode
            ? state[config.availableOptionsKey].length > 0
            : state[config.availableValuesKey].length > 0;

        if (!hasOptions) {
            refs.options.appendChild(
                createElement('p', 'app-checkbox-empty', config.emptyText)
            );
            return;
        }

        const options = state.serverMode
            ? state[config.availableOptionsKey]
            : state[config.availableValuesKey];

        options.forEach((option) => {
            const optionId = state.serverMode ? option.id : null;
            const optionName = state.serverMode ? option.name : option;
            const isSelected = state.serverMode
                ? state[config.selectedIdsKey].includes(optionId)
                : state[config.selectedValuesKey].includes(optionName);
            const checkboxOption = window.AppUi.createCheckboxOption({
                text: optionName,
                checked: isSelected,
                selected: isSelected
            });
            const optionLabel = checkboxOption.option;
            const checkbox = checkboxOption.checkbox;

            optionLabel.classList.toggle('is-selected', isSelected);
            checkbox.dataset.role = config.optionRole;
            checkbox.dataset[config.valueDatasetKey] = optionName;
            if (state.serverMode) {
                checkbox.dataset[config.idDatasetKey] = String(optionId);
            }

            refs.options.appendChild(optionLabel);
        });
    }

    function toggleValue(instance, config, rawValue, isSelected, callbacks) {
        const normalizedValue = String(rawValue || '').trim();
        if (!normalizedValue) {
            return;
        }

        const nextSelectedValues = new Set(instance.state[config.selectedValuesKey]);
        if (isSelected) {
            nextSelectedValues.add(normalizedValue);
        } else {
            nextSelectedValues.delete(normalizedValue);
        }

        instance.state[config.selectedValuesKey] = Array.from(nextSelectedValues)
            .sort((left, right) => left.localeCompare(right, 'ru'));
        callbacks?.applyPageFilters?.(instance.page);
    }

    function toggleId(instance, config, rawId, isSelected, callbacks) {
        const id = Number.parseInt(String(rawId || ''), 10);
        if (!Number.isInteger(id)) {
            return;
        }

        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        const nextSelectedIds = new Set(instance.state[config.selectedIdsKey]);
        if (isSelected) {
            nextSelectedIds.add(id);
        } else {
            nextSelectedIds.delete(id);
        }

        instance.state[config.selectedIdsKey] = Array.from(nextSelectedIds).sort((left, right) => left - right);
        const serverConfig = serverFilters.getConfig(instance.page);
        if (serverConfig) {
            serverConfig[config.selectedConfigKey] = [...instance.state[config.selectedIdsKey]];
        }
        instance.state.hasPendingServerNavigation = true;
        callbacks?.applyPageFilters?.(instance.page);
    }

    function clear(instance, config, callbacks) {
        if (instance.state.serverMode) {
            const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
            instance.state[config.selectedIdsKey] = [];
            const serverConfig = serverFilters.getConfig(instance.page);
            if (serverConfig) {
                serverConfig[config.selectedConfigKey] = [];
            }
            render(instance, config);
            instance.state.hasPendingServerNavigation = true;
            callbacks?.applyPageFilters?.(instance.page);
            return;
        }

        instance.state[config.selectedValuesKey] = [];
        render(instance, config);
        callbacks?.applyPageFilters?.(instance.page);
    }

    function createInstance(root, definition, {
        pageSelector,
        serverFilters = window.SurveyServerFilterState,
        filterPopover = window.SurveyFilterPopover,
        closeAllPopovers,
        setPopoverOpen,
        applyPageFilters
    } = {}) {
        const config = getConfig(definition?.pendingFilterName);
        if (!definition || !config || !(root instanceof Element)) {
            return null;
        }

        const page = root.closest(pageSelector);
        const tableBody = page?.querySelector('[data-role="main-table"] tbody');
        if (!page || !tableBody) {
            return null;
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
            handlers: {},
            dropdownController: null
        };

        const callbacks = { serverFilters, applyPageFilters };
        const setOpen = (isOpen) => setPopoverOpen?.(instance, isOpen) ?? filterPopover.setOpen(instance, isOpen);
        const commitServerFilter = () => {
            if (!instance.state.serverMode || !instance.state.hasPendingServerNavigation) {
                return;
            }

            instance.state.hasPendingServerNavigation = false;
            serverFilters.navigate(instance.page);
        };

        if (typeof window.AppUi?.createMultiselect === 'function'
            && instance.refs.trigger
            && instance.refs.popover) {
            const dropdown = window.AppUi.createMultiselect({
                root,
                trigger: instance.refs.trigger,
                menu: instance.refs.popover,
                openClass: 'is-open',
                hiddenClass: 'is-hidden',
                onOpen: () => {
                    closeAllPopovers?.(root);
                    filterPopover.applyOpenState(instance, true);
                },
                onClose: () => {
                    filterPopover.applyOpenState(instance, false);
                    commitServerFilter();
                }
            });
            instance.dropdownController = dropdown.controller;
        }

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const target = event.target instanceof Element ? event.target : null;
            if (!target) {
                return;
            }

            const trigger = target.closest(`[data-role="${definition.triggerRole}"]`);
            if (!instance.dropdownController && trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers?.(shouldOpen ? root : null);
                setOpen(shouldOpen);
                return;
            }

            if (target.closest(`[data-role="${definition.closeRole}"]`)) {
                event.preventDefault();
                setOpen(false);
                return;
            }

            if (target.closest(`[data-role="${definition.clearRole}"]`)) {
                event.preventDefault();
                clear(instance, config, callbacks);
            }
        };

        instance.handlers.change = function (event) {
            const target = event.target instanceof Element ? event.target : null;
            const option = target?.closest(`[data-role="${config.optionRole}"]`);
            if (!option || !root.contains(option)) {
                return;
            }

            if (instance.state.serverMode) {
                toggleId(instance, config, option.dataset[config.idDatasetKey], Boolean(option.checked), callbacks);
                return;
            }

            toggleValue(instance, config, option.dataset[config.valueDatasetKey], Boolean(option.checked), callbacks);
        };

        root.addEventListener('click', instance.handlers.click);
        root.addEventListener('change', instance.handlers.change);
        instance.destroy = function destroyCheckboxFilterInstance() {
            root.removeEventListener('click', instance.handlers.click);
            root.removeEventListener('change', instance.handlers.change);
            instance.dropdownController?.destroy?.();
        };

        render(instance, config);
        applyPageFilters?.(instance.page);
        return instance;
    }

    window.SurveyCheckboxFilter = {
        getConfig,
        getSelectedNames,
        createInstance,
        render,
        toggleValue,
        toggleId,
        clear
    };
})();
