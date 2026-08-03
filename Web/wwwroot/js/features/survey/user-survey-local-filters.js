export function createSurveyUserLocalFilters({
    contentHost,
    emptyTemplate,
    state,
    getContentRefs,
    getMonthLabel,
    setSelectOptions
}) {
    function ensureFilteredEmptyRow(tableBody, hasVisibleRows) {
        if (!tableBody || !emptyTemplate?.content?.firstElementChild) {
            return;
        }

        const existingEmptyRow = tableBody.querySelector('[data-role="user-survey-filter-empty-row"]');
        if (hasVisibleRows) {
            existingEmptyRow?.remove();
            return;
        }

        if (existingEmptyRow) {
            return;
        }

        const emptyRow = emptyTemplate.content.firstElementChild.cloneNode(true);
        emptyRow.dataset.role = 'user-survey-filter-empty-row';
        tableBody.appendChild(emptyRow);
    }

    function populateDateFilters() {
        const refs = getContentRefs();
        const rows = Array.from(contentHost.querySelectorAll('[data-role="user-survey-row"]'));

        const monthOptions = Array.from(new Set(rows.map((row) => row.dataset.filterMonth || '').filter(Boolean)))
            .sort()
            .map((value) => ({ value, label: getMonthLabel(value) }));

        const yearOptions = Array.from(new Set(rows.map((row) => row.dataset.filterYear || '').filter(Boolean)))
            .sort((left, right) => Number(right) - Number(left))
            .map((value) => ({ value, label: value }));

        state.monthFilter = setSelectOptions(refs.monthFilter, monthOptions, 'Все месяцы', state.monthFilter);
        state.yearFilter = setSelectOptions(refs.yearFilter, yearOptions, 'Все годы', state.yearFilter);
    }

    function applyLocalFilters() {
        const refs = getContentRefs();
        const rows = Array.from(contentHost.querySelectorAll('[data-role="user-survey-row"]'));

        if (!refs.tableBody || rows.length === 0) {
            return;
        }

        let visibleCount = 0;
        rows.forEach((row) => {
            const rowMonth = row.dataset.filterMonth || '';
            const rowYear = row.dataset.filterYear || '';
            const matchesMonth = !state.monthFilter || rowMonth === state.monthFilter;
            const matchesYear = !state.yearFilter || rowYear === state.yearFilter;
            const visible = matchesMonth && matchesYear;

            row.hidden = !visible;
            if (visible) {
                visibleCount += 1;
            }
        });

        const serverEmptyRow = refs.tableBody.querySelector('[data-role="user-survey-empty-row"]');
        if (serverEmptyRow && rows.length > 0) {
            serverEmptyRow.hidden = visibleCount > 0;
        }

        ensureFilteredEmptyRow(refs.tableBody, visibleCount > 0);
    }

    return {
        populateDateFilters,
        applyLocalFilters
    };
}
