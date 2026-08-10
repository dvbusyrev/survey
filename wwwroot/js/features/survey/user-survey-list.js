import {
    buildSurveyUserHistoryEntry,
    createSnapshotFromHost,
    createSnapshotFromTemplateElement,
    getMonthLabel,
    getSurveyId,
    getSurveyUserHistoryEntryFromLocation,
    mountSurveyUserModal,
    normalizeSurveyUserPathname,
    readSurveyUserActiveCountFromDom,
    readSurveyUserActiveCountFromSnapshot,
    setSelectOptions,
    syncSurveyUserActiveCountBadge
} from './user-survey-page-helpers.js';
import { createSurveyUserLocalFilters } from './user-survey-local-filters.js';
import { createSurveyUserRowTooltip } from './user-survey-row-tooltip.js';
import { fetchSurveyUserSnapshot } from './user-survey-snapshot-loader.js';

function readSurveyUserBootstrapData(root = document) {
    const bootstrapElement = root.querySelector('#survey-user-list-bootstrap')
        || root.querySelector('#user-archive-bootstrap')
        || document.getElementById('survey-user-list-bootstrap')
        || document.getElementById('user-archive-bootstrap');
    if (!bootstrapElement?.textContent) {
        return null;
    }

    try {
        return JSON.parse(bootstrapElement.textContent.trim());
    } catch (error) {
        console.error('Не удалось прочитать bootstrap-данные user survey:', error);
        return null;
    }
}

function mountSurveyUserListPage(page, bindSurveyUserListPage) {
    const bootstrapData = readSurveyUserBootstrapData(page);
    return bootstrapData
        ? bindSurveyUserListPage(bootstrapData, page)
        : null;
}

function renderSurveyUserChrome(initialData) {
    const chromeContext = typeof window.readAppChromeContext === 'function'
        ? window.readAppChromeContext()
        : null;
    const headerHost = document.getElementById('chrome-header');
    const footerHost = document.getElementById('chrome-footer');
    const props = {
        userRole: chromeContext?.userRole || initialData?.userRole,
        displayName: chromeContext?.displayName || initialData?.displayName,
        userName: chromeContext?.userName || initialData?.userName,
        organizationName: chromeContext?.organizationName || initialData?.organizationName
    };

    if (headerHost && typeof window.mountHeader === 'function') {
        window.mountHeader(headerHost, props);
    }

    if (footerHost && typeof window.mountFooter === 'function') {
        window.mountFooter(footerHost);
    }
}

function createSurveyUserListInteractionController({
    contentHost,
    state,
    rowTooltip,
    localFilters,
    openSurveyById,
    handleTabChange,
    loadTabSnapshot
} = {}) {
    if (!contentHost) {
        return { destroy: () => {} };
    }

    const tabActions = [
        ['tab-active', 'active'],
        ['tab-help', 'help'],
        ['tab-archived', 'archived']
    ];

    function getEventTarget(event) {
        return event.target instanceof Element ? event.target : null;
    }

    function belongsToPage(element) {
        return Boolean(element && contentHost.contains(element));
    }

    function readPositiveNumber(element, name) {
        const value = Number(element?.dataset?.[name] || 0);
        return Number.isFinite(value) && value > 0 ? value : 0;
    }

    function getClickedTab(target) {
        for (const [role, tab] of tabActions) {
            const button = target.closest(`[data-role="${role}"]`);
            if (belongsToPage(button)) {
                return tab;
            }
        }

        return null;
    }

    function handleClick(event) {
        const target = getEventTarget(event);
        if (!target) {
            return;
        }

        const tab = getClickedTab(target);
        if (tab) {
            event.preventDefault();
            handleTabChange?.(tab);
            return;
        }

        const actionButton = target.closest('[data-role="action"]');
        if (belongsToPage(actionButton)) {
            const surveyId = readPositiveNumber(actionButton, 'surveyId');
            if (surveyId) {
                openSurveyById?.(surveyId);
            }
            return;
        }

        const actionableRow = target.closest('[data-role="user-survey-row"][data-row-action]');
        if (belongsToPage(actionableRow) && !target.closest('button, a, input, select, textarea')) {
            const surveyId = readPositiveNumber(actionableRow, 'surveyId');
            if (surveyId) {
                rowTooltip?.hide?.();
                openSurveyById?.(surveyId);
            }
            return;
        }

        const paginationButton = target.closest('[data-role="pagination-page"]');
        if (belongsToPage(paginationButton)) {
            const targetPage = readPositiveNumber(paginationButton, 'page');
            if (!targetPage || targetPage === state?.currentSnapshot?.currentPage) {
                return;
            }

            event.preventDefault();
            loadTabSnapshot?.(state.activeTab, {
                page: targetPage,
                searchTerm: state.currentSnapshot.searchTerm,
                signedOnly: state.currentSnapshot.signedOnly,
                scrollToTableStart: true
            });
        }
    }

    function handleMouseOver(event) {
        const target = getEventTarget(event);
        const row = target?.closest('[data-role="user-survey-row"][data-hover-label]');
        if (!belongsToPage(row) || rowTooltip?.isActiveRow?.(row)) {
            return;
        }

        rowTooltip?.show?.(row, event);
    }

    function handleMouseMove(event) {
        if (!rowTooltip?.hasActiveRow?.()) {
            return;
        }

        rowTooltip.move(event);
    }

    function handleMouseOut(event) {
        if (!rowTooltip?.hasActiveRow?.() || rowTooltip.activeRowContains?.(event.relatedTarget)) {
            return;
        }

        rowTooltip.hide();
    }

    function handleSubmit(event) {
        const target = getEventTarget(event);
        const searchForm = target?.closest('[data-role="search-form"]');
        if (!belongsToPage(searchForm)) {
            return;
        }

        event.preventDefault();
        const searchInput = searchForm.querySelector('[data-role="search-input"]');
        const signedInput = searchForm.querySelector('[data-role="signed-filter-input"]');

        loadTabSnapshot?.(state.activeTab, {
            page: 1,
            searchTerm: searchInput?.value?.trim() || '',
            signedOnly: Boolean(signedInput?.checked)
        });
    }

    function handleChange(event) {
        const target = getEventTarget(event);
        if (!target) {
            return;
        }

        const monthFilter = target.closest('[data-role="month-filter"]');
        if (belongsToPage(monthFilter)) {
            state.monthFilter = monthFilter.value;
            localFilters?.applyLocalFilters?.();
            return;
        }

        const yearFilter = target.closest('[data-role="year-filter"]');
        if (belongsToPage(yearFilter)) {
            state.yearFilter = yearFilter.value;
            localFilters?.applyLocalFilters?.();
            return;
        }

        const signedInput = target.closest('[data-role="signed-filter-input"]');
        if (belongsToPage(signedInput)) {
            loadTabSnapshot?.('archived', {
                page: 1,
                searchTerm: state.currentSnapshot.searchTerm,
                signedOnly: signedInput.checked
            });
        }
    }

    const listeners = [
        ['click', handleClick],
        ['mouseover', handleMouseOver],
        ['mousemove', handleMouseMove],
        ['mouseout', handleMouseOut],
        ['submit', handleSubmit],
        ['change', handleChange]
    ];
    listeners.forEach(([type, handler]) => contentHost.addEventListener(type, handler));

    return {
        destroy() {
            listeners.forEach(([type, handler]) => {
                contentHost.removeEventListener(type, handler);
            });
        }
    };
}

function createSurveyUserHistoryController({ onTabChange } = {}) {
    function sync(tab, mode) {
        const entry = buildSurveyUserHistoryEntry(tab);
        if (!entry) {
            return;
        }

        const currentPath = normalizeSurveyUserPathname(window.location.pathname);
        const shouldKeepCurrentQuery = tab === 'archived'
            && currentPath === entry.url
            && window.location.search;
        const entryUrl = shouldKeepCurrentQuery
            ? `${entry.url}${window.location.search}`
            : entry.url;
        const nextState = { tab: entry.tab };

        if (mode === 'replace') {
            window.history.replaceState(nextState, '', entryUrl);
            return;
        }

        if (currentPath === entry.url
            && window.location.search === (shouldKeepCurrentQuery ? window.location.search : '')
            && window.history.state?.tab === nextState.tab) {
            return;
        }

        window.history.pushState(nextState, '', entryUrl);
    }

    function pushArchiveFilterQuery(queryString) {
        const normalizedQuery = String(queryString || '').replace(/^\?/, '').trim();
        const nextHistoryUrl = normalizedQuery ? `/archive?${normalizedQuery}` : '/archive';
        const currentUrl = `${normalizeSurveyUserPathname(window.location.pathname)}${window.location.search}`;

        if (currentUrl === nextHistoryUrl && window.history.state?.tab === 'archived') {
            window.history.replaceState({ tab: 'archived' }, '', nextHistoryUrl);
        } else {
            window.history.pushState({ tab: 'archived' }, '', nextHistoryUrl);
        }

        return normalizedQuery;
    }

    function handlePopState() {
        const entry = window.history.state?.tab
            ? buildSurveyUserHistoryEntry(window.history.state.tab)
            : getSurveyUserHistoryEntryFromLocation(window.location.pathname);

        if (!entry) {
            return;
        }

        onTabChange?.(entry.tab, { historyMode: 'none' });
    }

    function mount() {
        window.addEventListener('popstate', handlePopState);
    }

    function destroy() {
        window.removeEventListener('popstate', handlePopState);
    }

    return {
        sync,
        pushArchiveFilterQuery,
        mount,
        destroy
    };
}

function createSurveyUserListModalController({
    state,
    initialData,
    setError,
    isDisposed,
    onBackToList,
    onSurveySubmitted
}) {
    const modalState = {
        fillCleanup: null,
        answersCleanup: null,
        prefetchedHtml: null,
        openRequestId: 0
    };

    function cleanup(kind) {
        if (kind === 'fill' && typeof modalState.fillCleanup === 'function') {
            modalState.fillCleanup();
            modalState.fillCleanup = null;
        }

        if (kind === 'answers' && typeof modalState.answersCleanup === 'function') {
            modalState.answersCleanup();
            modalState.answersCleanup = null;
        }
    }

    function getModalConfig() {
        if (state.currentView === 'survey-fill') {
            return {
                kind: 'fill',
                hostSelector: '[data-role="fill-modal-host"]',
                title: 'Заполнение анкеты',
                mountPage: window.mountSurveyFillPage,
                extraOptions: {
                    onBack: onBackToList,
                    onSubmitted: () => onSurveySubmitted?.(state.currentSurvey)
                }
            };
        }

        if (state.currentView === 'check-answers') {
            return {
                kind: 'answers',
                hostSelector: '[data-role="answers-modal-host"]',
                title: 'Просмотр анкеты',
                mountPage: window.mountCheckAnswersPage,
                extraOptions: {}
            };
        }

        return null;
    }

    function render() {
        cleanup('fill');
        cleanup('answers');

        const config = getModalConfig();
        const modalHost = config ? document.querySelector(config.hostSelector) : null;
        if (!config || !state.currentSurvey || !modalHost || typeof config.mountPage !== 'function') {
            return;
        }

        const initialHtml = modalState.prefetchedHtml;
        modalState.prefetchedHtml = null;
        modalState[`${config.kind}Cleanup`] = mountSurveyUserModal(modalHost, {
            title: config.title,
            onClose: onBackToList,
            mountBody: (modalBodyHost, modalFooterHost) => config.mountPage(modalBodyHost, {
                survey: state.currentSurvey,
                organizationId: initialData.userOrganizationId,
                initialHtml,
                footerHost: modalFooterHost,
                ...config.extraOptions
            })
        });
    }

    async function open(survey, activeTab) {
        if (!survey) {
            return;
        }

        const surveyId = getSurveyId(survey);
        const targetView = activeTab === 'active' ? 'survey-fill' : 'check-answers';
        const requestId = modalState.openRequestId + 1;
        modalState.openRequestId = requestId;

        try {
            const prefetchedHtml = targetView === 'survey-fill'
                ? await window.fetchSurveyFillContentHtml?.(surveyId, initialData.userOrganizationId)
                : await window.fetchSurveyAnswersContentHtml?.(surveyId, initialData.userOrganizationId);

            if (isDisposed() || modalState.openRequestId !== requestId) {
                return;
            }

            modalState.prefetchedHtml = typeof prefetchedHtml === 'string' ? prefetchedHtml : null;
            state.currentSurvey = survey;
            state.currentView = targetView;
            render();
        } catch (error) {
            if (isDisposed() || modalState.openRequestId !== requestId) {
                return;
            }

            modalState.prefetchedHtml = null;
            setError(error?.message || 'Не удалось открыть анкету.');
        }
    }

    function closeToList() {
        modalState.openRequestId += 1;
        modalState.prefetchedHtml = null;
        state.currentView = 'survey-list';
        state.currentSurvey = null;
        render();
    }

    function destroy() {
        modalState.openRequestId += 1;
        cleanup('fill');
        cleanup('answers');
    }

    return {
        render,
        open,
        closeToList,
        destroy
    };
}

function registerSurveyUserListPage(bindSurveyUserListPage) {
    if (window.AppPageLifecycle?.register) {
        window.AppPageLifecycle.register(
            'survey-user-list',
            '[data-page="user-surveys"]',
            (page) => mountSurveyUserListPage(page, bindSurveyUserListPage)
        );
        return;
    }

    const page = document.querySelector('[data-page="user-surveys"]');
    if (page) {
        mountSurveyUserListPage(page, bindSurveyUserListPage);
    }
}

window.bindSurveyUserListPage = function bindSurveyUserListPage(initialData, pageRoot = null) {
    window.__surveyUserListController?.destroy?.();

    const contentHost = pageRoot || document.getElementById('default_content');
    const emptyTemplate = contentHost?.querySelector('#survey-user-empty-template');
    if (!contentHost) {
        return null;
    }

    let disposed = false;

    function remountPageEnhancements() {
        if (!(contentHost instanceof Element)) {
            return;
        }

        window.SurveysPage?.destroy?.(contentHost);
        window.SurveyFilters?.destroy?.(contentHost);
        window.SurveyFilters?.mount?.(contentHost);
    }

    const initialSnapshot = createSnapshotFromHost(contentHost);
    if (!initialSnapshot) {
        return;
    }

    const tabTemplateElements = {
        active: contentHost.querySelector('#survey-user-active-content-template')
            || document.getElementById('survey-user-active-content-template'),
        archived: contentHost.querySelector('#survey-user-archived-content-template')
            || document.getElementById('survey-user-archived-content-template'),
        help: contentHost.querySelector('#survey-user-help-content-template')
            || document.getElementById('survey-user-help-content-template')
    };

    const state = {
        activeTab: initialSnapshot.activeTab,
        currentView: 'survey-list',
        currentSurvey: null,
        currentSnapshot: initialSnapshot,
        loading: false,
        activeCount: readSurveyUserActiveCountFromDom(contentHost)
            ?? readSurveyUserActiveCountFromSnapshot(initialSnapshot)
            ?? 0,
        monthFilter: '',
        yearFilter: '',
        tabSnapshots: {
            active: initialSnapshot.activeTab === 'active' ? initialSnapshot : null,
            archived: initialSnapshot.activeTab === 'archived' ? initialSnapshot : null,
            help: initialSnapshot.activeTab === 'help' ? initialSnapshot : null
        }
    };

    renderSurveyUserChrome(initialData);

    let refreshPromise = null;
    const rowTooltip = createSurveyUserRowTooltip();

    function getContentRoot() {
        return contentHost.querySelector('[data-role="survey-user-content"]');
    }

    function getCachedTabSnapshot(tab) {
        if (state.tabSnapshots[tab]) {
            return state.tabSnapshots[tab];
        }

        const snapshot = createSnapshotFromTemplateElement(tabTemplateElements[tab]);
        if (snapshot) {
            state.tabSnapshots[tab] = snapshot;
        }

        return snapshot;
    }

    function updateActiveCountFromSnapshot(snapshot) {
        const nextCount = readSurveyUserActiveCountFromSnapshot(snapshot);
        if (nextCount !== null) {
            state.activeCount = nextCount;
        }
    }

    function syncActiveCountBadge() {
        syncSurveyUserActiveCountBadge(getContentRoot(), state.activeCount);
    }

    function getContentRefs() {
        const root = getContentRoot();
        return {
            root,
            searchForm: root?.querySelector('[data-role="search-form"]'),
            searchInput: root?.querySelector('[data-role="search-input"]'),
            monthFilter: root?.querySelector('[data-role="month-filter"]'),
            yearFilter: root?.querySelector('[data-role="year-filter"]'),
            signedInput: root?.querySelector('[data-role="signed-filter-input"]'),
            loading: root?.querySelector('[data-role="loading"]'),
            tableSection: root?.querySelector('[data-role="table-section"]'),
            tableBody: root?.querySelector('[data-role="survey-table-body"]'),
            pagination: root?.querySelector('[data-role="pagination"]'),
            errorWrap: root?.querySelector('[data-role="error"]'),
            errorText: root?.querySelector('[data-role="error-text"]')
        };
    }

    function scrollToTableSection() {
        const refs = getContentRefs();
        const target = refs.tableSection?.querySelector('table') || refs.tableSection;
        if (!target) {
            return;
        }

        target.scrollIntoView({
            block: 'start',
            behavior: 'auto'
        });
    }

    function setLoading(isLoading) {
        state.loading = isLoading;
        const refs = getContentRefs();
        refs.loading?.classList.toggle('u-hidden', !isLoading);

        if (refs.tableSection) {
            refs.tableSection.classList.toggle('u-hidden', isLoading);
        }
    }

    function setError(message) {
        const refs = getContentRefs();
        refs.errorText && (refs.errorText.textContent = '');
        refs.errorWrap?.classList.add('u-hidden');

        const rawMessage = String(message || '').trim();
        if (!rawMessage) {
            return;
        }

        const safeMessage = typeof window.normalizeClientErrorMessage === 'function'
            ? window.normalizeClientErrorMessage(rawMessage)
            : rawMessage;
        window.AppUi.notify(safeMessage, 'error', { title: 'Ошибка' });
    }

    const localFilters = createSurveyUserLocalFilters({
        contentHost,
        emptyTemplate,
        state,
        getContentRefs,
        getMonthLabel,
        setSelectOptions
    });

    const modals = createSurveyUserListModalController({
        state,
        initialData,
        setError,
        isDisposed: () => disposed,
        onBackToList: handleBackToList,
        onSurveySubmitted: handleSurveySubmitted
    });

    const historyController = createSurveyUserHistoryController({
        onTabChange: (tab, options) => handleTabChange(tab, null, options)
    });
    let interactionController = null;

    function applySnapshot(snapshot, options = {}, { replaceContent = false } = {}) {
        if (!snapshot || (replaceContent && !snapshot.template)) {
            return;
        }

        if (replaceContent) {
            rowTooltip.hide();
            contentHost.replaceChildren(snapshot.template.content.cloneNode(true));
            state.currentSnapshot = createSnapshotFromHost(contentHost) || snapshot;
        } else {
            state.currentSnapshot = snapshot;
        }

        const currentSnapshot = state.currentSnapshot;
        state.activeTab = currentSnapshot.activeTab;
        state.tabSnapshots[state.activeTab] = currentSnapshot;

        if (state.activeTab === 'active') {
            updateActiveCountFromSnapshot(currentSnapshot);
        }

        if (!options.preserveFilters) {
            state.monthFilter = '';
            state.yearFilter = '';
        }

        setLoading(false);
        setError('');
        localFilters.populateDateFilters();
        localFilters.applyLocalFilters();
        remountPageEnhancements();
        syncActiveCountBadge();
        modals.render();
    }

    function mountSnapshot(snapshot, options = {}) {
        applySnapshot(snapshot, options, { replaceContent: true });
    }

    function hydrateCurrentSnapshot(snapshot, options = {}) {
        applySnapshot(snapshot, options);
    }

    async function fetchSnapshot(tab, page, searchTerm, signedOnly, filterQuery = null) {
        return fetchSurveyUserSnapshot({
            tab,
            userId: initialData.userId,
            page,
            searchTerm,
            signedOnly,
            filterQuery
        });
    }

    async function loadTabSnapshot(tab, options = {}) {
        const currentSnapshot = state.tabSnapshots[tab];
        const page = options.page ?? currentSnapshot?.currentPage ?? 1;
        const searchTerm = options.searchTerm ?? currentSnapshot?.searchTerm ?? '';
        const signedOnly = tab === 'archived'
            ? Boolean(options.signedOnly ?? currentSnapshot?.signedOnly)
            : false;

        if (options.showLoading !== false && state.activeTab === tab) {
            setError('');
            setLoading(true);
        }

        try {
            const snapshot = await fetchSnapshot(tab, page, searchTerm, signedOnly, options.filterQuery ?? null);
            if (disposed) {
                return null;
            }
            state.tabSnapshots[tab] = snapshot;
            if (tab === 'active') {
                updateActiveCountFromSnapshot(snapshot);
                syncActiveCountBadge();
            }

            if (options.applyToCurrent !== false && state.activeTab === tab) {
                mountSnapshot(snapshot, { preserveFilters: options.preserveFilters === true });
                if (options.scrollToTableStart === true) {
                    scrollToTableSection();
                }
            }

            return snapshot;
        } catch (error) {
            if (disposed) {
                return null;
            }
            if (state.activeTab === tab) {
                setLoading(false);
                setError(error?.message || 'Не удалось загрузить анкеты.');
            } else {
                console.error('Ошибка фонового обновления списка анкет:', error);
            }
            return null;
        }
    }

    function openSurveyById(surveyId) {
        const survey = state.currentSnapshot.surveys.find((item) => getSurveyId(item) === surveyId);
        if (!survey) {
            return;
        }

        modals.open(survey, state.activeTab);
    }

    function handleBackToList() {
        modals.closeToList();
    }

    async function handleSurveySubmitted() {
        handleBackToList();
        await refreshAllSnapshots({ preserveFilters: true });
    }

    async function refreshAllSnapshots(options = {}) {
        if (refreshPromise) {
            return refreshPromise;
        }

        const activeSnapshot = state.tabSnapshots.active;
        const archivedSnapshot = state.tabSnapshots.archived;

        refreshPromise = Promise.all([
            loadTabSnapshot('active', {
                page: activeSnapshot?.currentPage ?? 1,
                searchTerm: activeSnapshot?.searchTerm ?? '',
                applyToCurrent: false,
                showLoading: state.activeTab === 'active'
            }),
            loadTabSnapshot('archived', {
                page: archivedSnapshot?.currentPage ?? 1,
                searchTerm: archivedSnapshot?.searchTerm ?? '',
                signedOnly: archivedSnapshot?.signedOnly ?? false,
                applyToCurrent: false,
                showLoading: state.activeTab === 'archived'
            })
        ]).finally(() => {
            refreshPromise = null;
        });

        const [nextActiveSnapshot, nextArchivedSnapshot] = await refreshPromise;
        if (disposed) {
            return null;
        }
        const currentSnapshot = state.activeTab === 'archived'
            ? nextArchivedSnapshot
            : (state.activeTab === 'active' ? nextActiveSnapshot : state.tabSnapshots.help);

        if (currentSnapshot) {
            mountSnapshot(currentSnapshot, { preserveFilters: options.preserveFilters === true });
        }

        return {
            active: nextActiveSnapshot,
            archived: nextArchivedSnapshot
        };
    }

    function handleTabChange(tab, _unused = null, options = {}) {
        options = options || {};

        const normalizedTab = tab === 'archived_surveys_for_user' ? 'archived' : tab;
        if (normalizedTab !== 'active' && normalizedTab !== 'archived' && normalizedTab !== 'help') {
            return;
        }

        state.activeTab = normalizedTab;
        state.currentView = 'survey-list';
        state.currentSurvey = null;
        state.monthFilter = '';
        state.yearFilter = '';

        if (options.historyMode !== 'none') {
            historyController.sync(normalizedTab, options.historyMode || 'push');
        }

        const cachedSnapshot = getCachedTabSnapshot(normalizedTab);
        if (cachedSnapshot) {
            mountSnapshot(cachedSnapshot);
            return;
        }

        loadTabSnapshot(normalizedTab, {
            page: 1,
            searchTerm: '',
            signedOnly: false,
            applyToCurrent: true
        });
    }

    interactionController = createSurveyUserListInteractionController({
        contentHost,
        state,
        rowTooltip,
        localFilters,
        openSurveyById,
        handleTabChange,
        loadTabSnapshot
    });

    historyController.sync(state.activeTab, 'replace');
    hydrateCurrentSnapshot(initialSnapshot);

    const refreshSurveyUserPageData = function refreshSurveyUserPageData(options = {}) {
        return refreshAllSnapshots({
            preserveFilters: options.preserveFilters !== false
        });
    };
    window.refreshSurveyUserPageData = refreshSurveyUserPageData;

    const refreshSurveyUserArchiveFilters = function refreshSurveyUserArchiveFilters(queryString, options = {}) {
        if (state.activeTab !== 'archived') {
            return;
        }

        const normalizedQuery = historyController.pushArchiveFilterQuery(queryString);
        loadTabSnapshot('archived', {
            page: 1,
            searchTerm: state.currentSnapshot.searchTerm,
            signedOnly: state.currentSnapshot.signedOnly,
            filterQuery: normalizedQuery,
            preserveFilters: true,
            scrollToTableStart: Boolean(options.scrollTargetSelector)
        });
    };
    window.refreshSurveyUserArchiveFilters = refreshSurveyUserArchiveFilters;

    historyController.mount();

    const destroy = () => {
        if (disposed) {
            return;
        }

        disposed = true;
        modals.destroy();
        rowTooltip.destroy();
        interactionController?.destroy?.();
        historyController.destroy();
        window.SurveysPage?.destroy?.(contentHost);
        window.SurveyFilters?.destroy?.(contentHost);

        if (window.refreshSurveyUserPageData === refreshSurveyUserPageData) {
            delete window.refreshSurveyUserPageData;
        }
        if (window.refreshSurveyUserArchiveFilters === refreshSurveyUserArchiveFilters) {
            delete window.refreshSurveyUserArchiveFilters;
        }
        if (window.__surveyUserListController?.destroy === destroy) {
            delete window.__surveyUserListController;
        }
    };

    window.__surveyUserListController = { destroy };
    return destroy;
};

registerSurveyUserListPage(window.bindSurveyUserListPage);
