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
            const optionLabel = createElement('label', 'app-checkbox-option');
            const checkbox = createElement('input', 'app-checkbox-input');
            const labelText = createElement('span', 'app-checkbox-text', optionName);
            const isSelected = state.serverMode
                ? state[config.selectedIdsKey].includes(optionId)
                : state[config.selectedValuesKey].includes(optionName);

            optionLabel.classList.toggle('is-selected', isSelected);
            checkbox.type = 'checkbox';
            checkbox.dataset.role = config.optionRole;
            checkbox.dataset[config.valueDatasetKey] = optionName;
            if (state.serverMode) {
                checkbox.dataset[config.idDatasetKey] = String(optionId);
            }
            checkbox.checked = isSelected;

            optionLabel.appendChild(checkbox);
            optionLabel.appendChild(labelText);
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
        render(instance, config);
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
        render(instance, config);
        serverFilters.navigate(instance.page, config.filterName);
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
            serverFilters.navigate(instance.page, config.filterName);
            return;
        }

        instance.state[config.selectedValuesKey] = [];
        render(instance, config);
        callbacks?.applyPageFilters?.(instance.page);
    }

    window.SurveyCheckboxFilter = {
        getConfig,
        getSelectedNames,
        render,
        toggleValue,
        toggleId,
        clear
    };
})();
