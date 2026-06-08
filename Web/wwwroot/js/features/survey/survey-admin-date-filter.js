(function () {
    const existingController = window.__surveyAdminDateFilterController;
    if (existingController && typeof existingController.destroy === 'function') {
        existingController.destroy();
    }

    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"], .app-page[data-page="surveys-archive"], .app-page[data-page="answers-list"], .app-page[data-page="user-surveys"]';
    const FILTER_SELECTOR = '[data-role="survey-date-filter"]';
    const ORGANIZATION_FILTER_SELECTOR = '[data-role="survey-organization-filter"]';
    const SURVEY_NAME_FILTER_SELECTOR = '[data-role="survey-name-filter"]';
    const SURVEY_ROW_SELECTOR = 'tr[data-survey-date-begin][data-survey-date-end]';
    const MONTH_NAMES = [
        'Январь',
        'Февраль',
        'Март',
        'Апрель',
        'Май',
        'Июнь',
        'Июль',
        'Август',
        'Сентябрь',
        'Октябрь',
        'Ноябрь',
        'Декабрь'
    ];
    const WEEKDAY_NAMES = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];
    const instances = new Map();
    const organizationInstances = new Map();
    const surveyNameInstances = new Map();
    const serverFilterConfigs = new WeakMap();
    const PENDING_OPEN_FILTER_STORAGE_KEY = 'surveyAdminPendingOpenFilter';
    let observer = null;

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function toIso(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return '';
        }

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    function parseIso(isoValue) {
        const match = String(isoValue || '').trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return null;
        }

        const year = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const day = Number.parseInt(match[3], 10);
        const date = new Date(year, month - 1, day);

        if (Number.isNaN(date.getTime())
            || date.getFullYear() !== year
            || date.getMonth() !== month - 1
            || date.getDate() !== day) {
            return null;
        }

        return date;
    }

    function shiftMonth(sourceDate, monthOffset) {
        const date = sourceDate instanceof Date
            ? new Date(sourceDate.getFullYear(), sourceDate.getMonth(), 1)
            : new Date();
        date.setMonth(date.getMonth() + monthOffset);
        return new Date(date.getFullYear(), date.getMonth(), 1);
    }

    function getMonthBounds(year, monthIndex) {
        const startDate = new Date(year, monthIndex, 1);
        const endDate = new Date(year, monthIndex + 1, 0);

        return {
            start: toIso(startDate),
            end: toIso(endDate)
        };
    }

    function getYearBounds(year) {
        return {
            start: `${year}-01-01`,
            end: `${year}-12-31`
        };
    }

    function getDecadeStart(year) {
        return Math.floor(year / 10) * 10;
    }

    function getDisplayDate(isoValue) {
        if (window.AppDate?.toDisplay) {
            return window.AppDate.toDisplay(isoValue);
        }

        const date = parseIso(isoValue);
        if (!date) {
            return '';
        }

        return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
    }

    function compareIso(left, right) {
        if (!left || !right) {
            return 0;
        }

        return left === right ? 0 : (left > right ? 1 : -1);
    }

    function isIsoWithin(isoValue, startIso, endIso) {
        return Boolean(isoValue)
            && (!startIso || compareIso(isoValue, startIso) >= 0)
            && (!endIso || compareIso(isoValue, endIso) <= 0);
    }

    function getRangeDescription(startIso, endIso) {
        if (!startIso || !endIso) {
            return '';
        }

        return `${getDisplayDate(startIso)} - ${getDisplayDate(endIso)}`;
    }

    function getMonthDescription(year, monthIndex) {
        return `${MONTH_NAMES[monthIndex]} ${year}`;
    }

    function getYearDescription(year) {
        return `${year} год`;
    }

    function createElement(tagName, className, textContent) {
        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }
        if (textContent !== undefined) {
            element.textContent = textContent;
        }
        return element;
    }

    function ensurePopoverHeader(root) {
        const popover = root.querySelector('[data-role="survey-date-filter-popover"]');
        const modeSwitch = root.querySelector('[data-role="survey-date-filter-mode-switch"]');
        if (!popover || !modeSwitch) {
            return;
        }

        let header = popover.querySelector('.survey-period-filter__header');
        if (!header) {
            header = createElement('div', 'survey-period-filter__header');
            popover.insertBefore(header, modeSwitch);
            header.appendChild(modeSwitch);
        }

        if (!modeSwitch.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]')) {
            const yearModeButton = createElement('button', 'survey-period-filter__mode-button', 'По году');
            yearModeButton.type = 'button';
            yearModeButton.dataset.role = 'survey-date-filter-mode';
            yearModeButton.dataset.mode = 'year';
            modeSwitch.insertBefore(yearModeButton, modeSwitch.firstChild);
        }

        if (!header.querySelector('[data-role="survey-date-filter-close"]')) {
            const closeButton = createElement('button', 'survey-period-filter__close-button modal-close');
            closeButton.type = 'button';
            closeButton.dataset.role = 'survey-date-filter-close';
            closeButton.setAttribute('aria-label', 'Закрыть фильтр');

            const closeIcon = createElement('i', 'fas fa-xmark');
            closeIcon.setAttribute('aria-hidden', 'true');
            closeButton.appendChild(closeIcon);

            header.appendChild(closeButton);
        }

        if (!popover.querySelector('[data-role="survey-date-filter-year-panel"]')) {
            const yearPanel = createElement('div', 'survey-period-filter__panel is-hidden');
            yearPanel.dataset.role = 'survey-date-filter-year-panel';

            const panelNav = createElement('div', 'survey-period-filter__panel-nav');

            const prevButton = createElement('button', 'survey-period-filter__nav-button');
            prevButton.type = 'button';
            prevButton.dataset.role = 'survey-date-filter-year-range-prev';
            prevButton.setAttribute('aria-label', 'Предыдущие годы');
            prevButton.appendChild(createElement('i', 'fas fa-chevron-left'));
            prevButton.firstChild?.setAttribute('aria-hidden', 'true');

            const title = createElement('span', 'survey-period-filter__panel-title');
            title.dataset.role = 'survey-date-filter-year-range-label';

            const nextButton = createElement('button', 'survey-period-filter__nav-button');
            nextButton.type = 'button';
            nextButton.dataset.role = 'survey-date-filter-year-range-next';
            nextButton.setAttribute('aria-label', 'Следующие годы');
            nextButton.appendChild(createElement('i', 'fas fa-chevron-right'));
            nextButton.firstChild?.setAttribute('aria-hidden', 'true');

            panelNav.appendChild(prevButton);
            panelNav.appendChild(title);
            panelNav.appendChild(nextButton);

            const yearsContainer = createElement('div', 'survey-period-filter__years');
            yearsContainer.dataset.role = 'survey-date-filter-years';

            yearPanel.appendChild(panelNav);
            yearPanel.appendChild(yearsContainer);

            const monthPanel = popover.querySelector('[data-role="survey-date-filter-month-panel"]');
            if (monthPanel) {
                popover.insertBefore(yearPanel, monthPanel);
            } else {
                popover.appendChild(yearPanel);
            }
        }
    }

    function cleanupDetachedInstances() {
        Array.from(instances.entries()).forEach(([root]) => {
            if (!document.contains(root)) {
                instances.delete(root);
            }
        });

        Array.from(organizationInstances.entries()).forEach(([root]) => {
            if (!document.contains(root)) {
                organizationInstances.delete(root);
            }
        });

        Array.from(surveyNameInstances.entries()).forEach(([root]) => {
            if (!document.contains(root)) {
                surveyNameInstances.delete(root);
            }
        });
    }

    function getPagesFromNode(node) {
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

    function getDateInstanceForPage(page) {
        return Array.from(instances.values()).find((instance) => instance.page === page) || null;
    }

    function getOrganizationInstanceForPage(page) {
        return Array.from(organizationInstances.values()).find((instance) => instance.page === page) || null;
    }

    function getSurveyNameInstanceForPage(page) {
        return Array.from(surveyNameInstances.values()).find((instance) => instance.page === page) || null;
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

    function getPageItemLabel(page) {
        return page?.dataset?.filterItemLabel || 'анкет';
    }

    function getPageDateSummary(page) {
        return page?.dataset?.filterDateSummary || 'у которых дата начала и дата конца попадают';
    }

    function parseIntegerList(values) {
        if (!Array.isArray(values)) {
            return [];
        }

        return values
            .map((value) => Number.parseInt(String(value), 10))
            .filter((value, index, array) => Number.isInteger(value) && array.indexOf(value) === index);
    }

    function getServerFilterConfig(page) {
        if (!(page instanceof Element)) {
            return null;
        }

        if (serverFilterConfigs.has(page)) {
            return serverFilterConfigs.get(page);
        }

        const bootstrapNode = page.querySelector('script[data-role="server-filter-bootstrap"]');
        if (!bootstrapNode) {
            serverFilterConfigs.set(page, null);
            return null;
        }

        try {
            const parsed = JSON.parse(bootstrapNode.textContent || '{}');
            const config = {
                basePath: String(parsed?.BasePath || parsed?.basePath || '').trim(),
                enableDateFilter: Boolean(parsed?.EnableDateFilter ?? parsed?.enableDateFilter),
                enableOrganizationFilter: Boolean(parsed?.EnableOrganizationFilter ?? parsed?.enableOrganizationFilter),
                enableSurveyFilter: Boolean(parsed?.EnableSurveyFilter ?? parsed?.enableSurveyFilter),
                organizationOptions: Array.isArray(parsed?.OrganizationOptions ?? parsed?.organizationOptions)
                    ? (parsed.OrganizationOptions ?? parsed.organizationOptions).map((option) => ({
                        id: Number.parseInt(String(option?.Id ?? option?.id ?? ''), 10),
                        name: String(option?.Name ?? option?.name ?? '').trim()
                    })).filter((option) => Number.isInteger(option.id) && option.name)
                    : [],
                selectedOrganizationIds: parseIntegerList(parsed?.SelectedOrganizationIds ?? parsed?.selectedOrganizationIds),
                surveyOptions: Array.isArray(parsed?.SurveyOptions ?? parsed?.surveyOptions)
                    ? (parsed.SurveyOptions ?? parsed.surveyOptions).map((option) => ({
                        id: Number.parseInt(String(option?.Id ?? option?.id ?? ''), 10),
                        name: String(option?.Name ?? option?.name ?? '').trim()
                    })).filter((option) => Number.isInteger(option.id) && option.name)
                    : [],
                selectedSurveyIds: parseIntegerList(parsed?.SelectedSurveyIds ?? parsed?.selectedSurveyIds),
                year: Number.isInteger(parsed?.Year) ? parsed.Year : Number.parseInt(String(parsed?.Year ?? parsed?.year ?? ''), 10),
                month: String(parsed?.Month ?? parsed?.month ?? '').trim(),
                dateFrom: String(parsed?.DateFrom ?? parsed?.dateFrom ?? '').trim(),
                dateTo: String(parsed?.DateTo ?? parsed?.dateTo ?? '').trim()
            };

            if (!Number.isInteger(config.year)) {
                config.year = null;
            }

            serverFilterConfigs.set(page, config);
            return config;
        } catch (error) {
            serverFilterConfigs.set(page, null);
            return null;
        }
    }

    function isServerFilterPage(page) {
        const config = getServerFilterConfig(page);
        return Boolean(config?.basePath);
    }

    function getServerFilterTabName(page) {
        switch (page?.dataset?.page) {
            case 'surveys-list':
                return 'get_surveys';
            case 'surveys-archive':
                return 'archived_surveys';
            case 'answers-list':
                return 'list_answers_users';
            default:
                return '';
        }
    }

    function getSelectedOptionNames(options, selectedIds) {
        const selectedIdSet = new Set(parseIntegerList(selectedIds));
        return options
            .filter((option) => selectedIdSet.has(option.id))
            .map((option) => option.name)
            .sort((left, right) => left.localeCompare(right, 'ru'));
    }

    function buildServerFilterUrl(page) {
        const config = getServerFilterConfig(page);
        if (!config?.basePath) {
            return '';
        }

        const currentPath = normalizeCurrentPath(window.location.pathname);
        const basePath = normalizeCurrentPath(config.basePath);
        const params = currentPath === basePath
            ? new URLSearchParams(window.location.search)
            : new URLSearchParams();

        ['page', 'organizationIds', 'surveyIds', 'year', 'month', 'dateFrom', 'dateTo'].forEach((key) => {
            params.delete(key);
        });

        if (config.selectedOrganizationIds.length > 0) {
            params.set('organizationIds', config.selectedOrganizationIds.join(','));
        }

        if (config.selectedSurveyIds.length > 0) {
            params.set('surveyIds', config.selectedSurveyIds.join(','));
        }

        if (Number.isInteger(config.year)) {
            params.set('year', String(config.year));
        } else if (config.month) {
            params.set('month', config.month);
        } else {
            if (config.dateFrom) {
                params.set('dateFrom', config.dateFrom);
            }

            if (config.dateTo) {
                params.set('dateTo', config.dateTo);
            }
        }

        const queryString = params.toString();
        return queryString
            ? `${config.basePath}?${queryString}`
            : config.basePath;
    }

    function rememberPendingOpenFilter(page, filterName) {
        const normalizedFilterName = String(filterName || '').trim();
        if (!normalizedFilterName) {
            return;
        }

        const config = getServerFilterConfig(page);
        const payload = {
            filterName: normalizedFilterName,
            pageName: page?.dataset?.page || '',
            basePath: normalizeCurrentPath(config?.basePath || window.location.pathname),
            createdAt: Date.now()
        };

        window.__surveyAdminPendingOpenFilter = payload;
        try {
            window.sessionStorage?.setItem(PENDING_OPEN_FILTER_STORAGE_KEY, JSON.stringify(payload));
        } catch (error) {
            // sessionStorage can be unavailable in restricted browser modes.
        }
    }

    function readPendingOpenFilter() {
        if (window.__surveyAdminPendingOpenFilter) {
            return window.__surveyAdminPendingOpenFilter;
        }

        try {
            const rawValue = window.sessionStorage?.getItem(PENDING_OPEN_FILTER_STORAGE_KEY);
            return rawValue ? JSON.parse(rawValue) : null;
        } catch (error) {
            return null;
        }
    }

    function clearPendingOpenFilter() {
        window.__surveyAdminPendingOpenFilter = null;
        try {
            window.sessionStorage?.removeItem(PENDING_OPEN_FILTER_STORAGE_KEY);
        } catch (error) {
            // sessionStorage can be unavailable in restricted browser modes.
        }
    }

    function consumePendingOpenFilter(page, filterName) {
        const payload = readPendingOpenFilter();
        if (!payload || payload.filterName !== filterName) {
            return false;
        }

        if (Date.now() - Number(payload.createdAt || 0) > 15000) {
            clearPendingOpenFilter();
            return false;
        }

        const config = getServerFilterConfig(page);
        const expectedPath = normalizeCurrentPath(config?.basePath || window.location.pathname);
        if (payload.basePath && payload.basePath !== expectedPath) {
            return false;
        }

        if (payload.pageName && payload.pageName !== (page?.dataset?.page || '')) {
            return false;
        }

        clearPendingOpenFilter();
        return true;
    }

    function normalizeCurrentPath(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    function navigateServerFilterPage(page, openFilterName = '') {
        const url = buildServerFilterUrl(page);
        if (!url) {
            return;
        }

        rememberPendingOpenFilter(page, openFilterName);

        const config = getServerFilterConfig(page);
        const queryIndex = url.indexOf('?');
        const queryString = queryIndex >= 0 ? url.slice(queryIndex + 1) : '';
        const tabName = getServerFilterTabName(page);
        const scrollTargetSelector = page?.dataset?.tableScrollTarget || '';

        if (typeof window.refreshAdminTab === 'function' && tabName) {
            window.refreshAdminTab(tabName, queryString || null, {
                scrollTargetSelector
            });
            return;
        }

        window.location.assign(url);
    }

    function syncServerDateFilterState(instance) {
        const config = getServerFilterConfig(instance?.page);
        if (!config) {
            return;
        }

        config.year = null;
        config.month = '';
        config.dateFrom = '';
        config.dateTo = '';

        if (instance.state.activeFilterType === 'year' && Number.isInteger(instance.state.activeYear)) {
            config.year = instance.state.activeYear;
            return;
        }

        if (instance.state.activeFilterType === 'month' && instance.state.activeMonth) {
            config.month = `${instance.state.activeMonth.year}-${pad(instance.state.activeMonth.monthIndex + 1)}`;
            return;
        }

        if (instance.state.activeFilterType === 'range' && instance.state.rangeStart && instance.state.rangeEnd) {
            config.dateFrom = instance.state.rangeStart;
            config.dateTo = instance.state.rangeEnd;
        }
    }

    function getInitialDateState(page, today) {
        const state = {
            isOpen: false,
            mode: 'month',
            monthViewYear: today.getFullYear(),
            yearViewStart: getDecadeStart(today.getFullYear()),
            rangeViewDate: new Date(today.getFullYear(), today.getMonth(), 1),
            activeFilterType: 'all',
            activeYear: null,
            activeMonth: null,
            rangeStart: '',
            rangeEnd: ''
        };
        const config = getServerFilterConfig(page);
        if (!config?.enableDateFilter) {
            return state;
        }

        if (Number.isInteger(config.year)) {
            state.activeFilterType = 'year';
            state.activeYear = config.year;
            state.monthViewYear = config.year;
            state.yearViewStart = getDecadeStart(config.year);
            return state;
        }

        const monthMatch = config.month.match(/^(\d{4})-(\d{2})$/);
        if (monthMatch) {
            const year = Number.parseInt(monthMatch[1], 10);
            const monthIndex = Number.parseInt(monthMatch[2], 10) - 1;
            if (Number.isInteger(year) && Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
                state.activeFilterType = 'month';
                state.activeMonth = { year, monthIndex };
                state.monthViewYear = year;
                state.yearViewStart = getDecadeStart(year);
                return state;
            }
        }

        if (config.dateFrom && config.dateTo) {
            state.activeFilterType = 'range';
            state.rangeStart = config.dateFrom;
            state.rangeEnd = config.dateTo;
            const rangeDate = parseIso(config.dateFrom);
            if (rangeDate) {
                state.rangeViewDate = new Date(rangeDate.getFullYear(), rangeDate.getMonth(), 1);
            }
        }

        return state;
    }

    function shouldHideCountSummary(page) {
        return page?.dataset?.filterHideCountSummary === 'true';
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

    function getCurrentRangeDisplayState(state) {
        if (state.mode === 'range' && state.rangeStart && !state.rangeEnd) {
            return { start: state.rangeStart, end: '' };
        }

        if (state.rangeStart && state.rangeEnd) {
            return { start: state.rangeStart, end: state.rangeEnd };
        }

        return { start: '', end: '' };
    }

    function getActiveFilterBounds(state) {
        if (state.activeFilterType === 'year' && Number.isInteger(state.activeYear)) {
            return getYearBounds(state.activeYear);
        }

        if (state.activeFilterType === 'month' && state.activeMonth) {
            return getMonthBounds(state.activeMonth.year, state.activeMonth.monthIndex);
        }

        if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            return {
                start: state.rangeStart,
                end: state.rangeEnd
            };
        }

        return null;
    }

    function getDataRows(instance) {
        return Array.from(instance.refs.tableBody?.querySelectorAll(SURVEY_ROW_SELECTOR) || []);
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

    function updateFilterSummary(instance, visibleCount, totalCount) {
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

    function updateOrganizationFilterSummary(instance, visibleCount, totalCount) {
        const selectedOrganizations = instance.state.serverMode
            ? getSelectedOptionNames(instance.state.availableOrganizationOptions, instance.state.selectedOrganizationIds)
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

    function updateSurveyNameFilterSummary(instance, visibleCount, totalCount) {
        const selectedSurveyNames = instance.state.serverMode
            ? getSelectedOptionNames(instance.state.availableSurveyOptions, instance.state.selectedSurveyIds)
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

    function updatePageSummaries(page, visibleCount, totalCount) {
        const dateInstance = getDateInstanceForPage(page);
        const organizationInstance = getOrganizationInstanceForPage(page);
        const surveyNameInstance = getSurveyNameInstanceForPage(page);

        if (dateInstance) {
            updateFilterSummary(dateInstance, visibleCount, totalCount);
        }

        if (organizationInstance) {
            updateOrganizationFilterSummary(organizationInstance, visibleCount, totalCount);
        }

        if (surveyNameInstance) {
            updateSurveyNameFilterSummary(surveyNameInstance, visibleCount, totalCount);
        }
    }

    function applyPageFilters(page) {
        const rows = getDataRowsFromPage(page);
        if (isServerFilterPage(page)) {
            updatePageSummaries(
                page,
                rows.length,
                Number.parseInt(String(page?.dataset?.totalCount || rows.length), 10) || rows.length
            );
            syncEmptyRow(page, rows, rows.length);
            return;
        }

        const totalCount = rows.length;
        const dateInstance = getDateInstanceForPage(page);
        const organizationInstance = getOrganizationInstanceForPage(page);
        const surveyNameInstance = getSurveyNameInstanceForPage(page);
        const bounds = dateInstance ? getActiveFilterBounds(dateInstance.state) : null;
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

    function updateCheckboxListHeight(container) {
        const list = container?.querySelector('.app-checkbox-list');
        if (!list) {
            return;
        }

        const listTop = list.getBoundingClientRect().top;
        const availableHeight = Math.max(160, window.innerHeight - listTop - 24);
        list.style.setProperty('--app-checkbox-list-max-height', `${availableHeight}px`);
    }

    function scheduleCheckboxListHeightUpdate(container) {
        window.requestAnimationFrame(() => updateCheckboxListHeight(container));
    }

    function setPopoverOpen(instance, isOpen) {
        instance.state.isOpen = Boolean(isOpen);
        instance.refs.trigger.setAttribute('aria-expanded', instance.state.isOpen ? 'true' : 'false');
        instance.refs.popover.classList.toggle('is-hidden', !instance.state.isOpen);
        if (instance.state.isOpen) {
            scheduleCheckboxListHeightUpdate(instance.refs.popover);
        }
    }

    function closeAllPopovers(exceptRoot = null) {
        cleanupDetachedInstances();

        instances.forEach((instance, root) => {
            if (root === exceptRoot) {
                return;
            }

            setPopoverOpen(instance, false);
        });

        organizationInstances.forEach((instance, root) => {
            if (root === exceptRoot) {
                return;
            }

            setPopoverOpen(instance, false);
        });

        surveyNameInstances.forEach((instance, root) => {
            if (root === exceptRoot) {
                return;
            }

            setPopoverOpen(instance, false);
        });
    }

    function renderModeSwitch(instance) {
        const { state, refs } = instance;
        refs.yearPanel.classList.toggle('is-hidden', state.mode !== 'year');
        refs.monthPanel.classList.toggle('is-hidden', state.mode !== 'month');
        refs.rangePanel.classList.toggle('is-hidden', state.mode !== 'range');

        refs.yearModeButton.classList.toggle('is-active', state.mode === 'year');
        refs.monthModeButton.classList.toggle('is-active', state.mode === 'month');
        refs.rangeModeButton.classList.toggle('is-active', state.mode === 'range');
    }

    function renderYearPanel(instance) {
        const { state, refs } = instance;
        refs.yearRangeLabel.textContent = `${state.yearViewStart} - ${state.yearViewStart + 9}`;
        refs.yearsContainer.textContent = '';

        for (let year = state.yearViewStart; year < state.yearViewStart + 10; year += 1) {
            const yearButton = createElement('button', 'survey-period-filter__year-button', String(year));
            yearButton.type = 'button';
            yearButton.dataset.role = 'survey-date-filter-year';
            yearButton.dataset.year = String(year);

            if (state.activeFilterType === 'year' && state.activeYear === year) {
                yearButton.classList.add('is-selected');
            }

            refs.yearsContainer.appendChild(yearButton);
        }
    }

    function renderMonthPanel(instance) {
        const { state, refs } = instance;
        refs.yearLabel.textContent = String(state.monthViewYear);
        refs.monthsContainer.textContent = '';

        MONTH_NAMES.forEach((monthName, monthIndex) => {
            const monthButton = createElement('button', 'survey-period-filter__month-button', monthName);
            monthButton.type = 'button';
            monthButton.dataset.role = 'survey-date-filter-month';
            monthButton.dataset.monthIndex = String(monthIndex);

            const isSelected = state.activeFilterType === 'month'
                && state.activeMonth
                && state.activeMonth.year === state.monthViewYear
                && state.activeMonth.monthIndex === monthIndex;
            monthButton.classList.toggle('is-selected', isSelected);

            refs.monthsContainer.appendChild(monthButton);
        });
    }

    function buildWeekdayRow() {
        const weekdaysRow = createElement('div', 'survey-period-filter__weekday-row');
        WEEKDAY_NAMES.forEach((weekday) => {
            weekdaysRow.appendChild(createElement('span', 'survey-period-filter__weekday', weekday));
        });
        return weekdaysRow;
    }

    function buildDayButton(instance, isoValue, displayState) {
        const dayButton = createElement('button', 'survey-period-filter__day-button');
        const date = parseIso(isoValue);
        dayButton.type = 'button';
        dayButton.dataset.role = 'survey-date-filter-day';
        dayButton.dataset.dateIso = isoValue;
        dayButton.textContent = date ? String(date.getDate()) : '';

        if (date && toIso(new Date()) === isoValue) {
            dayButton.classList.add('is-today');
        }

        if (displayState.start && isoValue === displayState.start) {
            dayButton.classList.add('is-range-start');
        }

        if (displayState.end && isoValue === displayState.end) {
            dayButton.classList.add('is-range-end');
        }

        if (displayState.start && displayState.end && compareIso(isoValue, displayState.start) > 0 && compareIso(isoValue, displayState.end) < 0) {
            dayButton.classList.add('is-in-range');
        }

        if (!displayState.end && displayState.start && isoValue === displayState.start) {
            dayButton.classList.add('is-range-single');
        }

        return dayButton;
    }

    function buildCalendarCard(instance, monthDate, displayState) {
        const card = createElement('div', 'survey-period-filter__calendar-card');
        const title = createElement(
            'h4',
            'survey-period-filter__calendar-title',
            getMonthDescription(monthDate.getFullYear(), monthDate.getMonth())
        );
        const weekdaysRow = buildWeekdayRow();
        const daysGrid = createElement('div', 'survey-period-filter__days-grid');
        const firstDayIndex = (new Date(monthDate.getFullYear(), monthDate.getMonth(), 1).getDay() + 6) % 7;
        const daysInMonth = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0).getDate();

        for (let index = 0; index < firstDayIndex; index += 1) {
            daysGrid.appendChild(createElement('span', 'survey-period-filter__day-placeholder'));
        }

        for (let day = 1; day <= daysInMonth; day += 1) {
            const isoValue = toIso(new Date(monthDate.getFullYear(), monthDate.getMonth(), day));
            daysGrid.appendChild(buildDayButton(instance, isoValue, displayState));
        }

        card.appendChild(title);
        card.appendChild(weekdaysRow);
        card.appendChild(daysGrid);
        return card;
    }

    function renderRangePanel(instance) {
        const { state, refs } = instance;
        const displayState = getCurrentRangeDisplayState(state);
        const firstMonth = new Date(state.rangeViewDate.getFullYear(), state.rangeViewDate.getMonth(), 1);
        const secondMonth = shiftMonth(firstMonth, 1);

        refs.rangeLabel.textContent = `${getMonthDescription(firstMonth.getFullYear(), firstMonth.getMonth())} - ${getMonthDescription(secondMonth.getFullYear(), secondMonth.getMonth())}`;
        refs.calendars.textContent = '';
        refs.calendars.appendChild(buildCalendarCard(instance, firstMonth, displayState));
        refs.calendars.appendChild(buildCalendarCard(instance, secondMonth, displayState));

        if (state.rangeStart && !state.rangeEnd) {
            if (refs.hint) {
                refs.hint.textContent = `Начало диапазона: ${getDisplayDate(state.rangeStart)}. Выберите конечную дату.`;
            }
            return;
        }

        if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            if (refs.hint) {
                refs.hint.textContent = shouldHideCountSummary(instance.page)
                    ? ''
                    : `Выбран диапазон: ${getRangeDescription(state.rangeStart, state.rangeEnd)}.`;
            }
            return;
        }

        if (refs.hint) {
            refs.hint.textContent = 'Выберите начальную и конечную дату периода.';
        }
    }

    function renderOrganizationPanel(instance) {
        const { state, refs } = instance;
        refs.options.textContent = '';

        const hasOptions = state.serverMode
            ? state.availableOrganizationOptions.length > 0
            : state.availableOrganizations.length > 0;

        if (!hasOptions) {
            refs.options.appendChild(
                createElement('p', 'app-checkbox-empty', 'Организации для фильтрации не найдены.')
            );
            return;
        }

        const options = state.serverMode
            ? state.availableOrganizationOptions
            : state.availableOrganizations;

        options.forEach((option) => {
            const organizationId = state.serverMode ? option.id : null;
            const organizationName = state.serverMode ? option.name : option;
            const optionLabel = createElement('label', 'app-checkbox-option');
            const checkbox = createElement('input', 'app-checkbox-input');
            const labelText = createElement('span', 'app-checkbox-text', organizationName);
            const isSelected = state.serverMode
                ? state.selectedOrganizationIds.includes(organizationId)
                : state.selectedOrganizations.includes(organizationName);

            optionLabel.classList.toggle('is-selected', isSelected);
            checkbox.type = 'checkbox';
            checkbox.dataset.role = 'survey-organization-filter-option';
            checkbox.dataset.organizationName = organizationName;
            if (state.serverMode) {
                checkbox.dataset.organizationId = String(organizationId);
            }
            checkbox.checked = isSelected;

            optionLabel.appendChild(checkbox);
            optionLabel.appendChild(labelText);
            refs.options.appendChild(optionLabel);
        });
    }

    function renderSurveyNamePanel(instance) {
        const { state, refs } = instance;
        refs.options.textContent = '';

        const hasOptions = state.serverMode
            ? state.availableSurveyOptions.length > 0
            : state.availableSurveyNames.length > 0;

        if (!hasOptions) {
            refs.options.appendChild(
                createElement('p', 'app-checkbox-empty', 'Анкеты для фильтрации не найдены.')
            );
            return;
        }

        const options = state.serverMode
            ? state.availableSurveyOptions
            : state.availableSurveyNames;

        options.forEach((option) => {
            const surveyId = state.serverMode ? option.id : null;
            const surveyName = state.serverMode ? option.name : option;
            const optionLabel = createElement('label', 'app-checkbox-option');
            const checkbox = createElement('input', 'app-checkbox-input');
            const labelText = createElement('span', 'app-checkbox-text', surveyName);
            const isSelected = state.serverMode
                ? state.selectedSurveyIds.includes(surveyId)
                : state.selectedSurveyNames.includes(surveyName);

            optionLabel.classList.toggle('is-selected', isSelected);
            checkbox.type = 'checkbox';
            checkbox.dataset.role = 'survey-name-filter-option';
            checkbox.dataset.surveyName = surveyName;
            if (state.serverMode) {
                checkbox.dataset.surveyId = String(surveyId);
            }
            checkbox.checked = isSelected;

            optionLabel.appendChild(checkbox);
            optionLabel.appendChild(labelText);
            refs.options.appendChild(optionLabel);
        });
    }

    function render(instance) {
        renderModeSwitch(instance);
        renderYearPanel(instance);
        renderMonthPanel(instance);
        renderRangePanel(instance);
    }

    function clearFilter(instance) {
        instance.state.activeFilterType = 'all';
        instance.state.activeYear = null;
        instance.state.activeMonth = null;
        instance.state.rangeStart = '';
        instance.state.rangeEnd = '';
        render(instance);
        if (isServerFilterPage(instance.page)) {
            syncServerDateFilterState(instance);
            navigateServerFilterPage(instance.page, 'date');
            return;
        }

        applyFilter(instance);
    }

    function applyYearFilter(instance, year) {
        const { state } = instance;
        const isSameYear = state.activeFilterType === 'year' && state.activeYear === year;

        if (isSameYear) {
            clearFilter(instance);
            return;
        }

        state.activeFilterType = 'year';
        state.activeYear = year;
        state.monthViewYear = year;
        state.yearViewStart = getDecadeStart(year);
        render(instance);
        if (isServerFilterPage(instance.page)) {
            syncServerDateFilterState(instance);
            navigateServerFilterPage(instance.page, 'date');
            return;
        }
        applyFilter(instance);
    }

    function applyMonthFilter(instance, monthIndex) {
        const { state } = instance;
        const isSameMonth = state.activeFilterType === 'month'
            && state.activeMonth
            && state.activeMonth.year === state.monthViewYear
            && state.activeMonth.monthIndex === monthIndex;

        if (isSameMonth) {
            clearFilter(instance);
            return;
        }

        state.activeFilterType = 'month';
        state.activeYear = null;
        state.activeMonth = {
            year: state.monthViewYear,
            monthIndex
        };
        render(instance);
        if (isServerFilterPage(instance.page)) {
            syncServerDateFilterState(instance);
            navigateServerFilterPage(instance.page, 'date');
            return;
        }
        applyFilter(instance);
    }

    function handleRangeSelection(instance, isoValue) {
        const { state } = instance;

        if (!state.rangeStart || state.rangeEnd) {
            state.rangeStart = isoValue;
            state.rangeEnd = '';
            state.activeFilterType = 'all';
            render(instance);
            if (isServerFilterPage(instance.page)) {
                return;
            }
            applyFilter(instance);
            return;
        }

        if (compareIso(isoValue, state.rangeStart) < 0) {
            state.rangeEnd = state.rangeStart;
            state.rangeStart = isoValue;
        } else {
            state.rangeEnd = isoValue;
        }

        state.activeFilterType = 'range';
        state.activeYear = null;
        render(instance);
        if (isServerFilterPage(instance.page)) {
            syncServerDateFilterState(instance);
            navigateServerFilterPage(instance.page, 'date');
            return;
        }
        applyFilter(instance);
    }

    function renderOrganization(instance) {
        renderOrganizationPanel(instance);
    }

    function renderSurveyName(instance) {
        renderSurveyNamePanel(instance);
    }

    function toggleOrganizationSelection(instance, organizationName, isSelected) {
        const normalizedName = String(organizationName || '').trim();
        if (!normalizedName) {
            return;
        }

        const nextSelectedOrganizations = new Set(instance.state.selectedOrganizations);
        if (isSelected) {
            nextSelectedOrganizations.add(normalizedName);
        } else {
            nextSelectedOrganizations.delete(normalizedName);
        }

        instance.state.selectedOrganizations = Array.from(nextSelectedOrganizations)
            .sort((left, right) => left.localeCompare(right, 'ru'));
        renderOrganization(instance);
        applyPageFilters(instance.page);
    }

    function toggleOrganizationIdSelection(instance, organizationId, isSelected) {
        if (!Number.isInteger(organizationId)) {
            return;
        }

        const nextSelectedOrganizationIds = new Set(instance.state.selectedOrganizationIds);
        if (isSelected) {
            nextSelectedOrganizationIds.add(organizationId);
        } else {
            nextSelectedOrganizationIds.delete(organizationId);
        }

        instance.state.selectedOrganizationIds = Array.from(nextSelectedOrganizationIds).sort((left, right) => left - right);
        const config = getServerFilterConfig(instance.page);
        if (config) {
            config.selectedOrganizationIds = [...instance.state.selectedOrganizationIds];
        }
        renderOrganization(instance);
        navigateServerFilterPage(instance.page, 'organization');
    }

    function toggleSurveyNameSelection(instance, surveyName, isSelected) {
        const normalizedName = String(surveyName || '').trim();
        if (!normalizedName) {
            return;
        }

        const nextSelectedSurveyNames = new Set(instance.state.selectedSurveyNames);
        if (isSelected) {
            nextSelectedSurveyNames.add(normalizedName);
        } else {
            nextSelectedSurveyNames.delete(normalizedName);
        }

        instance.state.selectedSurveyNames = Array.from(nextSelectedSurveyNames)
            .sort((left, right) => left.localeCompare(right, 'ru'));
        renderSurveyName(instance);
        applyPageFilters(instance.page);
    }

    function toggleSurveyIdSelection(instance, surveyId, isSelected) {
        if (!Number.isInteger(surveyId)) {
            return;
        }

        const nextSelectedSurveyIds = new Set(instance.state.selectedSurveyIds);
        if (isSelected) {
            nextSelectedSurveyIds.add(surveyId);
        } else {
            nextSelectedSurveyIds.delete(surveyId);
        }

        instance.state.selectedSurveyIds = Array.from(nextSelectedSurveyIds).sort((left, right) => left - right);
        const config = getServerFilterConfig(instance.page);
        if (config) {
            config.selectedSurveyIds = [...instance.state.selectedSurveyIds];
        }
        renderSurveyName(instance);
        navigateServerFilterPage(instance.page, 'survey');
    }

    function bindInstance(root) {
        if (!(root instanceof Element) || instances.has(root)) {
            return;
        }

        ensurePopoverHeader(root);

        const page = root.closest(PAGE_SELECTOR);
        const tableBody = page?.querySelector('[data-role="main-table"] tbody');
        if (!page || !tableBody) {
            return;
        }

        const today = new Date();
        const instance = {
            root,
            page,
            state: getInitialDateState(page, today),
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
                clearButton: root.querySelector('[data-role="survey-date-filter-clear"]'),
                tableBody,
                emptyRow: page.querySelector('[data-role="survey-filter-empty-row"]')
            },
            handlers: {}
        };

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
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-range-prev"]')) {
                event.preventDefault();
                instance.state.yearViewStart -= 10;
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-range-next"]')) {
                event.preventDefault();
                instance.state.yearViewStart += 10;
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-prev"]')) {
                event.preventDefault();
                instance.state.monthViewYear -= 1;
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-year-next"]')) {
                event.preventDefault();
                instance.state.monthViewYear += 1;
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-range-prev"]')) {
                event.preventDefault();
                instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, -1);
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-range-next"]')) {
                event.preventDefault();
                instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, 1);
                render(instance);
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
                    applyYearFilter(instance, selectedYear);
                }
                return;
            }

            const monthButton = event.target.closest('[data-role="survey-date-filter-month"]');
            if (monthButton && root.contains(monthButton)) {
                event.preventDefault();
                const monthIndex = Number.parseInt(monthButton.dataset.monthIndex || '', 10);
                if (Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
                    applyMonthFilter(instance, monthIndex);
                }
                return;
            }

            const dayButton = event.target.closest('[data-role="survey-date-filter-day"]');
            if (dayButton && root.contains(dayButton)) {
                event.preventDefault();
                const isoValue = dayButton.dataset.dateIso || '';
                if (parseIso(isoValue)) {
                    handleRangeSelection(instance, isoValue);
                }
                return;
            }

            if (event.target.closest('[data-role="survey-date-filter-clear"]')) {
                event.preventDefault();
                clearFilter(instance);
            }
        };

        root.addEventListener('click', instance.handlers.click);

        instances.set(root, instance);
        render(instance);
        applyFilter(instance);
        if (consumePendingOpenFilter(page, 'date')) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }
    }

    function bindOrganizationInstance(root) {
        if (!(root instanceof Element) || organizationInstances.has(root)) {
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
            state: {
                isOpen: false,
                serverMode: isServerFilterPage(page),
                availableOrganizations: collectAvailableOrganizations(page),
                availableOrganizationOptions: getServerFilterConfig(page)?.organizationOptions || [],
                selectedOrganizations: [],
                selectedOrganizationIds: [...(getServerFilterConfig(page)?.selectedOrganizationIds || [])]
            },
            refs: {
                trigger: root.querySelector('[data-role="survey-organization-filter-trigger"]'),
                label: root.querySelector('[data-role="survey-organization-filter-label"]'),
                popover: root.querySelector('[data-role="survey-organization-filter-popover"]'),
                options: root.querySelector('[data-role="survey-organization-filter-options"]'),
                summary: root.querySelector('[data-role="survey-organization-filter-summary"]'),
                clearButton: root.querySelector('[data-role="survey-organization-filter-clear"]')
            },
            handlers: {}
        };

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const trigger = event.target.closest('[data-role="survey-organization-filter-trigger"]');
            if (trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers(shouldOpen ? root : null);
                setPopoverOpen(instance, shouldOpen);
                return;
            }

            if (event.target.closest('[data-role="survey-organization-filter-close"]')) {
                event.preventDefault();
                setPopoverOpen(instance, false);
                return;
            }

            if (event.target.closest('[data-role="survey-organization-filter-clear"]')) {
                event.preventDefault();
                if (instance.state.serverMode) {
                    instance.state.selectedOrganizationIds = [];
                    const config = getServerFilterConfig(instance.page);
                    if (config) {
                        config.selectedOrganizationIds = [];
                    }
                    renderOrganization(instance);
                    navigateServerFilterPage(instance.page, 'organization');
                    return;
                }

                instance.state.selectedOrganizations = [];
                renderOrganization(instance);
                applyPageFilters(instance.page);
            }
        };

        instance.handlers.change = function (event) {
            const option = event.target.closest('[data-role="survey-organization-filter-option"]');
            if (!option || !root.contains(option)) {
                return;
            }

            if (instance.state.serverMode) {
                toggleOrganizationIdSelection(
                    instance,
                    Number.parseInt(option.dataset.organizationId || '', 10),
                    Boolean(option.checked)
                );
                return;
            }

            toggleOrganizationSelection(instance, option.dataset.organizationName || '', Boolean(option.checked));
        };

        root.addEventListener('click', instance.handlers.click);
        root.addEventListener('change', instance.handlers.change);

        organizationInstances.set(root, instance);
        renderOrganization(instance);
        applyPageFilters(instance.page);
        if (consumePendingOpenFilter(page, 'organization')) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }
    }

    function bindSurveyNameInstance(root) {
        if (!(root instanceof Element) || surveyNameInstances.has(root)) {
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
            state: {
                isOpen: false,
                serverMode: isServerFilterPage(page),
                availableSurveyNames: collectAvailableSurveyNames(page),
                availableSurveyOptions: getServerFilterConfig(page)?.surveyOptions || [],
                selectedSurveyNames: [],
                selectedSurveyIds: [...(getServerFilterConfig(page)?.selectedSurveyIds || [])]
            },
            refs: {
                trigger: root.querySelector('[data-role="survey-name-filter-trigger"]'),
                label: root.querySelector('[data-role="survey-name-filter-label"]'),
                popover: root.querySelector('[data-role="survey-name-filter-popover"]'),
                options: root.querySelector('[data-role="survey-name-filter-options"]'),
                summary: root.querySelector('[data-role="survey-name-filter-summary"]'),
                clearButton: root.querySelector('[data-role="survey-name-filter-clear"]')
            },
            handlers: {}
        };

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const trigger = event.target.closest('[data-role="survey-name-filter-trigger"]');
            if (trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers(shouldOpen ? root : null);
                setPopoverOpen(instance, shouldOpen);
                return;
            }

            if (event.target.closest('[data-role="survey-name-filter-close"]')) {
                event.preventDefault();
                setPopoverOpen(instance, false);
                return;
            }

            if (event.target.closest('[data-role="survey-name-filter-clear"]')) {
                event.preventDefault();
                if (instance.state.serverMode) {
                    instance.state.selectedSurveyIds = [];
                    const config = getServerFilterConfig(instance.page);
                    if (config) {
                        config.selectedSurveyIds = [];
                    }
                    renderSurveyName(instance);
                    navigateServerFilterPage(instance.page, 'survey');
                    return;
                }

                instance.state.selectedSurveyNames = [];
                renderSurveyName(instance);
                applyPageFilters(instance.page);
            }
        };

        instance.handlers.change = function (event) {
            const option = event.target.closest('[data-role="survey-name-filter-option"]');
            if (!option || !root.contains(option)) {
                return;
            }

            if (instance.state.serverMode) {
                toggleSurveyIdSelection(
                    instance,
                    Number.parseInt(option.dataset.surveyId || '', 10),
                    Boolean(option.checked)
                );
                return;
            }

            toggleSurveyNameSelection(instance, option.dataset.surveyName || '', Boolean(option.checked));
        };

        root.addEventListener('click', instance.handlers.click);
        root.addEventListener('change', instance.handlers.change);

        surveyNameInstances.set(root, instance);
        renderSurveyName(instance);
        applyPageFilters(instance.page);
        if (consumePendingOpenFilter(page, 'survey')) {
            closeAllPopovers(root);
            setPopoverOpen(instance, true);
        }
    }

    function bindAvailablePages(root = document) {
        cleanupDetachedInstances();
        const pages = root === document
            ? Array.from(document.querySelectorAll(PAGE_SELECTOR))
            : getPagesFromNode(root);

        pages.forEach((page) => {
            const dateFilterRoot = page.querySelector(FILTER_SELECTOR);
            if (dateFilterRoot) {
                bindInstance(dateFilterRoot);
            }

            const organizationFilterRoot = page.querySelector(ORGANIZATION_FILTER_SELECTOR);
            if (organizationFilterRoot) {
                bindOrganizationInstance(organizationFilterRoot);
            }

            const surveyNameFilterRoot = page.querySelector(SURVEY_NAME_FILTER_SELECTOR);
            if (surveyNameFilterRoot) {
                bindSurveyNameInstance(surveyNameFilterRoot);
            }
        });
    }

    function handleDocumentClick(event) {
        cleanupDetachedInstances();

        let clickedInsideFilter = false;
        instances.forEach((instance, root) => {
            if (root.contains(event.target)) {
                clickedInsideFilter = true;
            }
        });

        organizationInstances.forEach((instance, root) => {
            if (root.contains(event.target)) {
                clickedInsideFilter = true;
            }
        });

        surveyNameInstances.forEach((instance, root) => {
            if (root.contains(event.target)) {
                clickedInsideFilter = true;
            }
        });

        if (!clickedInsideFilter) {
            closeAllPopovers();
        }
    }

    function handleDocumentKeydown(event) {
        if (event.key === 'Escape') {
            closeAllPopovers();
        }
    }

    function destroy() {
        instances.forEach((instance, root) => {
            if (instance.handlers?.click) {
                root.removeEventListener('click', instance.handlers.click);
            }
        });
        instances.clear();

        organizationInstances.forEach((instance, root) => {
            if (instance.handlers?.click) {
                root.removeEventListener('click', instance.handlers.click);
            }
            if (instance.handlers?.change) {
                root.removeEventListener('change', instance.handlers.change);
            }
        });
        organizationInstances.clear();

        surveyNameInstances.forEach((instance, root) => {
            if (instance.handlers?.click) {
                root.removeEventListener('click', instance.handlers.click);
            }
            if (instance.handlers?.change) {
                root.removeEventListener('change', instance.handlers.change);
            }
        });
        surveyNameInstances.clear();

        if (observer) {
            observer.disconnect();
            observer = null;
        }

        serverFilterConfigs.clear?.();

        document.removeEventListener('click', handleDocumentClick);
        document.removeEventListener('keydown', handleDocumentKeydown);
    }

    window.__surveyAdminDateFilterController = {
        destroy
    };

    document.addEventListener('click', handleDocumentClick);
    document.addEventListener('keydown', handleDocumentKeydown);

    if (typeof MutationObserver !== 'undefined' && document.body) {
        observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    bindAvailablePages(node);
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            bindAvailablePages(document);
        });
        return;
    }

    bindAvailablePages(document);
})();
