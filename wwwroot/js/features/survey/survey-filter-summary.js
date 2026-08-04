(function () {
    if (window.SurveyFilterSummary) {
        return;
    }

    const {
        getRangeDescription,
        getMonthDescription,
        getYearDescription
    } = window.SurveyFilterCore;

    function getPageItemLabel(page) {
        return page?.dataset?.filterItemLabel || 'анкет';
    }

    function getPageDateSummary(page) {
        return page?.dataset?.filterDateSummary || 'у которых дата начала и дата конца попадают';
    }

    function shouldHideCountSummary(page) {
        return page?.dataset?.filterHideCountSummary === 'true';
    }

    function getOrganizationFilterLabel(selectedOrganizations) {
        if (!Array.isArray(selectedOrganizations) || selectedOrganizations.length === 0) {
            return 'Фильтр по организациям';
        }

        return `Организации: ${selectedOrganizations.length}`;
    }

    function getSurveyNameFilterLabel(selectedSurveyNames) {
        if (!Array.isArray(selectedSurveyNames) || selectedSurveyNames.length === 0) {
            return 'Фильтр по анкетам';
        }

        return `Анкеты: ${selectedSurveyNames.length}`;
    }

    function updateDate(instance, visibleCount, totalCount) {
        const { state, refs } = instance;
        const itemLabel = getPageItemLabel(instance.page);
        const dateSummary = getPageDateSummary(instance.page);
        const hideCountSummary = shouldHideCountSummary(instance.page);
        let label = 'Фильтр по периоду';
        let summary = hideCountSummary ? '' : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;

        if (state.activeFilterType === 'year' && Number.isInteger(state.activeYear)) {
            const yearLabel = getYearDescription(state.activeYear);
            label = yearLabel;
            if (!hideCountSummary) {
                summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в ${yearLabel}.`;
            }
        } else if (state.activeFilterType === 'month' && state.activeMonth) {
            const monthLabel = getMonthDescription(state.activeMonth.year, state.activeMonth.monthIndex);
            label = monthLabel;
            if (!hideCountSummary) {
                summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в ${monthLabel}.`;
            }
        } else if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            const rangeLabel = getRangeDescription(state.rangeStart, state.rangeEnd);
            label = rangeLabel;
            if (!hideCountSummary) {
                summary = `Показано ${visibleCount} из ${totalCount} ${itemLabel}, ${dateSummary} в период ${rangeLabel}.`;
            }
        }

        refs.label.textContent = label;
        if (refs.summary) {
            refs.summary.textContent = summary;
        }
        refs.clearButton.disabled = state.activeFilterType === 'all'
            && !Number.isInteger(state.activeYear)
            && !state.activeMonth
            && !state.rangeStart
            && !state.rangeEnd;
    }

    function updateOrganization(instance, visibleCount, totalCount, serverFilters) {
        const selectedOrganizations = instance.state.serverMode
            ? serverFilters.getSelectedOptionNames(instance.state.availableOrganizationOptions, instance.state.selectedOrganizationIds)
            : instance.state.selectedOrganizations;
        const label = getOrganizationFilterLabel(selectedOrganizations);
        const itemLabel = getPageItemLabel(instance.page);
        const hideCountSummary = shouldHideCountSummary(instance.page);
        let summary = hideCountSummary ? '' : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;

        if (selectedOrganizations.length === 1) {
            summary = hideCountSummary
                ? `Организация: ${selectedOrganizations[0]}.`
                : `Показано ${visibleCount} из ${totalCount} ${itemLabel} для организации ${selectedOrganizations[0]}.`;
        } else if (selectedOrganizations.length > 1) {
            summary = hideCountSummary
                ? `Выбрано организаций: ${selectedOrganizations.length}.`
                : `Показано ${visibleCount} из ${totalCount} ${itemLabel} для ${selectedOrganizations.length} организаций.`;
        }

        instance.refs.label.textContent = label;
        if (instance.refs.summary) {
            instance.refs.summary.textContent = summary;
        }
        instance.refs.clearButton.disabled = instance.state.serverMode
            ? instance.state.selectedOrganizationIds.length === 0
            : selectedOrganizations.length === 0;
    }

    function updateSurveyName(instance, visibleCount, totalCount, serverFilters) {
        const selectedSurveyNames = instance.state.serverMode
            ? serverFilters.getSelectedOptionNames(instance.state.availableSurveyOptions, instance.state.selectedSurveyIds)
            : instance.state.selectedSurveyNames;
        const label = getSurveyNameFilterLabel(selectedSurveyNames);
        const itemLabel = getPageItemLabel(instance.page);
        const hideCountSummary = shouldHideCountSummary(instance.page);
        let summary = hideCountSummary ? '' : `Показано ${visibleCount} из ${totalCount} ${itemLabel}.`;

        if (selectedSurveyNames.length === 1) {
            summary = hideCountSummary
                ? `Анкета: ${selectedSurveyNames[0]}.`
                : `Показано ${visibleCount} из ${totalCount} ${itemLabel} по анкете ${selectedSurveyNames[0]}.`;
        } else if (selectedSurveyNames.length > 1) {
            summary = hideCountSummary
                ? `Выбрано анкет: ${selectedSurveyNames.length}.`
                : `Показано ${visibleCount} из ${totalCount} ${itemLabel} по ${selectedSurveyNames.length} анкетам.`;
        }

        instance.refs.label.textContent = label;
        if (instance.refs.summary) {
            instance.refs.summary.textContent = summary;
        }
        instance.refs.clearButton.disabled = instance.state.serverMode
            ? instance.state.selectedSurveyIds.length === 0
            : selectedSurveyNames.length === 0;
    }

    window.SurveyFilterSummary = {
        getPageItemLabel,
        getPageDateSummary,
        shouldHideCountSummary,
        getOrganizationFilterLabel,
        getSurveyNameFilterLabel,
        updateDate,
        updateOrganization,
        updateSurveyName
    };
})();
