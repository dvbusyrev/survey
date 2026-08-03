(function () {
    if (window.SurveyRowFiltering) {
        return;
    }

    const SURVEY_ROW_SELECTOR = 'tr[data-survey-date-begin][data-survey-date-end]';

    function getRows(page) {
        return Array.from(page?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
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

    function getRowSurveyName(row) {
        return String(row?.dataset?.surveyName || '').trim();
    }

    function collectAvailableOrganizations(page) {
        return Array.from(new Set(
            getRows(page)
                .flatMap((row) => parseRowOrganizations(row))
                .filter(Boolean)
        )).sort((left, right) => left.localeCompare(right, 'ru'));
    }

    function collectAvailableSurveyNames(page) {
        return Array.from(new Set(
            getRows(page)
                .map((row) => getRowSurveyName(row))
                .filter(Boolean)
        )).sort((left, right) => left.localeCompare(right, 'ru'));
    }

    function getVisibleCount(rows) {
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

    function applyLocalFilters(page, { dateBounds = null, selectedOrganizations = [], selectedSurveyNames = [], isIsoWithin } = {}) {
        const rows = getRows(page);
        rows.forEach((row) => {
            const beginIso = row.dataset.surveyDateBegin || '';
            const endIso = row.dataset.surveyDateEnd || '';
            const matchesDate = !dateBounds
                || (isIsoWithin(beginIso, dateBounds.start, dateBounds.end)
                    && isIsoWithin(endIso, dateBounds.start, dateBounds.end));
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

        const visibleCount = getVisibleCount(rows);
        syncEmptyRow(page, rows, visibleCount);

        return {
            rows,
            visibleCount,
            totalCount: rows.length
        };
    }

    window.SurveyRowFiltering = {
        getRows,
        collectAvailableOrganizations,
        collectAvailableSurveyNames,
        syncEmptyRow,
        applyLocalFilters
    };
})();
