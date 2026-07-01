(function () {
    if (window.SurveyServerFilterState) {
        return;
    }

    const { pad } = window.SurveyFilterCore || {};
    const serverFilterConfigs = new WeakMap();
    const PENDING_OPEN_FILTER_STORAGE_KEY = 'surveyAdminPendingOpenFilter';

    function parseIntegerList(values) {
        if (!Array.isArray(values)) {
            return [];
        }

        return values
            .map((value) => Number.parseInt(String(value), 10))
            .filter((value, index, array) => Number.isInteger(value) && array.indexOf(value) === index);
    }

    function getConfig(page) {
        if (!(page instanceof Element)) {
            return null;
        }

        const bootstrapNode = page.querySelector('script[data-role="server-filter-bootstrap"]');
        const bootstrapRaw = bootstrapNode?.textContent || '';
        const cachedConfig = serverFilterConfigs.get(page);
        if (cachedConfig && cachedConfig.raw === bootstrapRaw) {
            return cachedConfig.config;
        }

        if (!bootstrapNode) {
            serverFilterConfigs.set(page, { raw: '', config: null });
            return null;
        }

        try {
            const parsed = JSON.parse(bootstrapRaw || '{}');
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

            serverFilterConfigs.set(page, { raw: bootstrapRaw, config });
            return config;
        } catch (error) {
            serverFilterConfigs.set(page, { raw: bootstrapRaw, config: null });
            return null;
        }
    }

    function isServerPage(page) {
        const config = getConfig(page);
        return Boolean(config?.basePath);
    }

    function getTabName(page) {
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

    function normalizeCurrentPath(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    function buildUrl(page) {
        const config = getConfig(page);
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

        const config = getConfig(page);
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

        const config = getConfig(page);
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

    function navigate(page, openFilterName = '') {
        const url = buildUrl(page);
        if (!url) {
            return;
        }

        rememberPendingOpenFilter(page, openFilterName);

        const queryIndex = url.indexOf('?');
        const queryString = queryIndex >= 0 ? url.slice(queryIndex + 1) : '';
        const tabName = getTabName(page);
        const scrollTargetSelector = page?.dataset?.tableScrollTarget || '';

        if (typeof window.refreshAdminTab === 'function' && tabName) {
            window.refreshAdminTab(tabName, queryString || null, {
                scrollTargetSelector
            });
            return;
        }

        if (page?.dataset?.page === 'user-surveys') {
            if (typeof window.refreshSurveyUserArchiveFilters === 'function') {
                window.refreshSurveyUserArchiveFilters(queryString || null, {
                    openFilterName,
                    scrollTargetSelector
                });
            }
            return;
        }

        window.location.assign(url);
    }

    function syncDateState(page, state) {
        const config = getConfig(page);
        if (!config) {
            return;
        }

        config.year = null;
        config.month = '';
        config.dateFrom = '';
        config.dateTo = '';

        if (state.activeFilterType === 'year' && Number.isInteger(state.activeYear)) {
            config.year = state.activeYear;
            return;
        }

        if (state.activeFilterType === 'month' && state.activeMonth) {
            config.month = `${state.activeMonth.year}-${pad(state.activeMonth.monthIndex + 1)}`;
            return;
        }

        if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            config.dateFrom = state.rangeStart;
            config.dateTo = state.rangeEnd;
        }
    }

    window.SurveyServerFilterState = {
        getConfig,
        isServerPage,
        getSelectedOptionNames,
        navigate,
        consumePendingOpenFilter,
        syncDateState
    };
})();
