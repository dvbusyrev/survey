import {
    buildSurveyUserHistoryEntry,
    createSnapshotFromHost,
    createSnapshotFromHtml,
    createSnapshotFromTemplateElement,
    getMonthLabel,
    getSurveyId,
    getSurveyUserHistoryEntryFromLocation,
    mountSurveyUserModal,
    normalizeSurveyUserPathname,
    setSelectOptions
} from './user-survey-page-helpers.js';

const mountSurveyFillPage = window.mountSurveyFillPage;
const mountCheckAnswersPage = window.mountCheckAnswersPage;
const fetchSurveyFillContentHtml = window.fetchSurveyFillContentHtml;
const fetchSurveyAnswersContentHtml = window.fetchSurveyAnswersContentHtml;

window.bindSurveyUserListPage = function bindSurveyUserListPage(initialData) {
    const contentHost = document.getElementById('default_content');
    const emptyTemplate = document.getElementById('survey-user-empty-template');
    if (!contentHost) {
        return;
    }

    const initialSnapshot = createSnapshotFromHost(contentHost);
    if (!initialSnapshot) {
        return;
    }

    const state = {
        activeTab: initialSnapshot.activeTab,
        currentView: 'survey-list',
        currentSurvey: null,
        currentSnapshot: initialSnapshot,
        loading: false,
        monthFilter: '',
        yearFilter: '',
        tabSnapshots: {
            active: initialSnapshot.activeTab === 'active' ? initialSnapshot : createSnapshotFromTemplateElement(document.getElementById('survey-user-active-content-template')),
            archived: initialSnapshot.activeTab === 'archived' ? initialSnapshot : createSnapshotFromTemplateElement(document.getElementById('survey-user-archived-content-template'))
        }
    };

    const modalState = {
        fillCleanup: null,
        answersCleanup: null,
        prefetchedHtml: null,
        openRequestId: 0
    };

    function getContentRoot() {
        return contentHost.querySelector('[data-role="survey-user-content"]');
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
            prevPage: root?.querySelector('[data-role="prev-page"]'),
            nextPage: root?.querySelector('[data-role="next-page"]'),
            errorWrap: root?.querySelector('[data-role="error"]'),
            errorText: root?.querySelector('[data-role="error-text"]')
        };
    }

    function renderChrome() {
        const headerHost = document.getElementById('chrome-header');
        const navHost = document.getElementById('chrome-navigation');
        const footerHost = document.getElementById('chrome-footer');

        if (headerHost && typeof window.mountHeader === 'function') {
            window.mountHeader(headerHost, {
                userRole: initialData.userRole,
                displayName: initialData.displayName,
                userName: initialData.userName,
                organizationName: initialData.organizationName
            });
        }

        if (navHost && typeof window.mountNavigation === 'function') {
            window.mountNavigation(navHost, {
                openTab: handleTabChange,
                activeTab: state.activeTab === 'archived' ? 'archived_surveys_for_user' : state.activeTab,
                userRole: initialData.userRole,
                userId: initialData.userId
            });
        }

        if (footerHost && typeof window.mountFooter === 'function') {
            window.mountFooter(footerHost);
        }
    }

    function cleanupModal(kind) {
        if (kind === 'fill' && typeof modalState.fillCleanup === 'function') {
            modalState.fillCleanup();
            modalState.fillCleanup = null;
        }

        if (kind === 'answers' && typeof modalState.answersCleanup === 'function') {
            modalState.answersCleanup();
            modalState.answersCleanup = null;
        }
    }

    function renderModals() {
        cleanupModal('fill');
        cleanupModal('answers');

        const fillModalHost = document.querySelector('[data-role="fill-modal-host"]');
        const answersModalHost = document.querySelector('[data-role="answers-modal-host"]');

        if (state.currentView === 'survey-fill' && state.currentSurvey && fillModalHost) {
            const initialHtml = modalState.prefetchedHtml;
            modalState.prefetchedHtml = null;
            modalState.fillCleanup = mountSurveyUserModal(fillModalHost, {
                onClose: handleBackToList,
                mountBody: (modalBodyHost) => (typeof mountSurveyFillPage === 'function'
                    ? mountSurveyFillPage(modalBodyHost, {
                        survey: state.currentSurvey,
                        organizationId: initialData.userOrganizationId,
                        userRole: initialData.userRole,
                        initialHtml,
                        onBack: handleBackToList,
                        onSubmitted: () => handleSurveySubmitted(state.currentSurvey)
                    })
                    : null)
            });
        }

        if (state.currentView === 'check-answers' && state.currentSurvey && answersModalHost) {
            const initialHtml = modalState.prefetchedHtml;
            modalState.prefetchedHtml = null;
            modalState.answersCleanup = mountSurveyUserModal(answersModalHost, {
                onClose: handleBackToList,
                mountBody: (modalBodyHost) => (typeof mountCheckAnswersPage === 'function'
                    ? mountCheckAnswersPage(modalBodyHost, {
                        survey: state.currentSurvey,
                        organizationId: initialData.userOrganizationId,
                        userRole: initialData.userRole,
                        initialHtml,
                        onBack: handleBackToList
                    })
                    : null)
            });
        }
    }

    function syncHistory(tab, mode) {
        const entry = buildSurveyUserHistoryEntry(tab);
        if (!entry) {
            return;
        }

        const nextState = { tab: entry.tab };
        if (mode === 'replace') {
            window.history.replaceState(nextState, '', entry.url);
            return;
        }

        const currentPath = normalizeSurveyUserPathname(window.location.pathname);
        if (currentPath === entry.url && window.history.state?.tab === nextState.tab) {
            return;
        }

        window.history.pushState(nextState, '', entry.url);
    }

    function setLoading(isLoading) {
        state.loading = isLoading;
        const refs = getContentRefs();
        refs.loading?.classList.toggle('u-hidden', !isLoading);

        if (refs.tableSection) {
            refs.tableSection.style.display = isLoading ? 'none' : '';
        }
    }

    function setError(message) {
        const refs = getContentRefs();
        refs.errorWrap?.classList.toggle('u-hidden', !message);

        if (refs.errorText) {
            refs.errorText.textContent = message || '';
        }
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

    function mountSnapshot(snapshot, options = {}) {
        if (!snapshot?.template) {
            return;
        }

        contentHost.replaceChildren(snapshot.template.content.cloneNode(true));
        state.currentSnapshot = createSnapshotFromHost(contentHost) || snapshot;
        state.activeTab = state.currentSnapshot.activeTab;
        state.tabSnapshots[state.activeTab] = state.currentSnapshot;

        if (!options.preserveFilters) {
            state.monthFilter = '';
            state.yearFilter = '';
        }

        setLoading(false);
        setError('');
        populateDateFilters();
        applyLocalFilters();
        renderChrome();
        renderModals();
    }

    async function fetchSnapshot(tab, page, searchTerm, signedOnly) {
        const endpoint = tab === 'active'
            ? `/my-surveys?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}`
            : `/my-surveys/archive/${initialData.userId}?page=${page}&searchTerm=${encodeURIComponent(searchTerm || '')}&signedOnly=${signedOnly ? 'true' : 'false'}`;

        const response = await fetch(endpoint, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        if (!response.ok) {
            throw new Error('Ошибка загрузки данных анкет');
        }

        const html = await response.text();
        const snapshot = createSnapshotFromHtml(html);
        if (!snapshot) {
            throw new Error('Не удалось построить содержимое страницы анкет');
        }

        return snapshot;
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
            const snapshot = await fetchSnapshot(tab, page, searchTerm, signedOnly);
            state.tabSnapshots[tab] = snapshot;

            if (options.applyToCurrent !== false && state.activeTab === tab) {
                mountSnapshot(snapshot, { preserveFilters: options.preserveFilters === true });
            }

            return snapshot;
        } catch (error) {
            if (state.activeTab === tab) {
                setLoading(false);
                setError(error?.message || 'Ошибка загрузки данных анкет');
            } else {
                console.error('Ошибка фонового обновления списка анкет:', error);
            }
            return null;
        }
    }

    async function openSurveyById(surveyId) {
        const survey = state.currentSnapshot.surveys.find((item) => getSurveyId(item) === surveyId);
        if (!survey) {
            return;
        }

        const targetView = state.activeTab === 'active' ? 'survey-fill' : 'check-answers';
        const requestId = modalState.openRequestId + 1;
        modalState.openRequestId = requestId;

        try {
            const prefetchedHtml = targetView === 'survey-fill'
                ? await fetchSurveyFillContentHtml?.(surveyId, initialData.userOrganizationId)
                : await fetchSurveyAnswersContentHtml?.(surveyId, initialData.userOrganizationId);

            if (modalState.openRequestId !== requestId) {
                return;
            }

            modalState.prefetchedHtml = typeof prefetchedHtml === 'string' ? prefetchedHtml : null;
            state.currentSurvey = survey;
            state.currentView = targetView;
            renderModals();
        } catch (error) {
            if (modalState.openRequestId !== requestId) {
                return;
            }

            modalState.prefetchedHtml = null;
            setError(error?.message || 'Не удалось открыть анкету');
        }
    }

    function handleBackToList() {
        modalState.openRequestId += 1;
        modalState.prefetchedHtml = null;
        state.currentView = 'survey-list';
        state.currentSurvey = null;
        renderModals();
    }

    async function handleSurveySubmitted() {
        handleBackToList();

        const activeSnapshot = state.tabSnapshots.active;
        const archivedSnapshot = state.tabSnapshots.archived;

        await loadTabSnapshot('active', {
            page: activeSnapshot?.currentPage ?? 1,
            searchTerm: activeSnapshot?.searchTerm ?? '',
            applyToCurrent: state.activeTab === 'active'
        });

        await loadTabSnapshot('archived', {
            page: archivedSnapshot?.currentPage ?? 1,
            searchTerm: archivedSnapshot?.searchTerm ?? '',
            signedOnly: archivedSnapshot?.signedOnly ?? false,
            applyToCurrent: state.activeTab === 'archived'
        });

        if (state.activeTab === 'active' && state.tabSnapshots.active) {
            mountSnapshot(state.tabSnapshots.active, { preserveFilters: true });
        }
    }

    function handleTabChange(tab, _unused = null, options = {}) {
        options = options || {};

        if (tab === 'help') {
            window.location.href = '/help';
            return;
        }

        const normalizedTab = tab === 'archived_surveys_for_user' ? 'archived' : tab;
        if (normalizedTab !== 'active' && normalizedTab !== 'archived') {
            return;
        }

        state.activeTab = normalizedTab;
        state.currentView = 'survey-list';
        state.currentSurvey = null;
        state.monthFilter = '';
        state.yearFilter = '';

        if (options.historyMode !== 'none') {
            syncHistory(normalizedTab, options.historyMode || 'push');
        }

        const cachedSnapshot = state.tabSnapshots[normalizedTab];
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

    function handleClick(event) {
        const tabActiveButton = event.target.closest('[data-role="tab-active"]');
        if (tabActiveButton && contentHost.contains(tabActiveButton)) {
            event.preventDefault();
            handleTabChange('active');
            return;
        }

        const tabArchivedButton = event.target.closest('[data-role="tab-archived"]');
        if (tabArchivedButton && contentHost.contains(tabArchivedButton)) {
            event.preventDefault();
            handleTabChange('archived');
            return;
        }

        const actionButton = event.target.closest('[data-role="action"]');
        if (actionButton && contentHost.contains(actionButton)) {
            const surveyId = Number(actionButton.dataset.surveyId || 0);
            if (Number.isFinite(surveyId) && surveyId > 0) {
                openSurveyById(surveyId);
            }
            return;
        }

        const prevPageButton = event.target.closest('[data-role="prev-page"]');
        if (prevPageButton && contentHost.contains(prevPageButton) && !prevPageButton.disabled) {
            loadTabSnapshot(state.activeTab, {
                page: Math.max(1, state.currentSnapshot.currentPage - 1),
                searchTerm: state.currentSnapshot.searchTerm,
                signedOnly: state.currentSnapshot.signedOnly
            });
            return;
        }

        const nextPageButton = event.target.closest('[data-role="next-page"]');
        if (nextPageButton && contentHost.contains(nextPageButton) && !nextPageButton.disabled) {
            loadTabSnapshot(state.activeTab, {
                page: state.currentSnapshot.currentPage + 1,
                searchTerm: state.currentSnapshot.searchTerm,
                signedOnly: state.currentSnapshot.signedOnly
            });
        }
    }

    function handleDoubleClick(event) {
        const row = event.target.closest('[data-role="user-survey-row"]');
        if (!row || !contentHost.contains(row) || event.target.closest('button')) {
            return;
        }

        const surveyId = Number(row.dataset.surveyId || 0);
        if (Number.isFinite(surveyId) && surveyId > 0) {
            openSurveyById(surveyId);
        }
    }

    function handleSubmit(event) {
        const searchForm = event.target.closest('[data-role="search-form"]');
        if (!searchForm || !contentHost.contains(searchForm)) {
            return;
        }

        event.preventDefault();
        const searchInput = searchForm.querySelector('[data-role="search-input"]');
        const signedInput = searchForm.querySelector('[data-role="signed-filter-input"]');

        loadTabSnapshot(state.activeTab, {
            page: 1,
            searchTerm: searchInput?.value?.trim() || '',
            signedOnly: Boolean(signedInput?.checked)
        });
    }

    function handleChange(event) {
        const monthFilter = event.target.closest('[data-role="month-filter"]');
        if (monthFilter && contentHost.contains(monthFilter)) {
            state.monthFilter = monthFilter.value;
            applyLocalFilters();
            return;
        }

        const yearFilter = event.target.closest('[data-role="year-filter"]');
        if (yearFilter && contentHost.contains(yearFilter)) {
            state.yearFilter = yearFilter.value;
            applyLocalFilters();
            return;
        }

        const signedInput = event.target.closest('[data-role="signed-filter-input"]');
        if (signedInput && contentHost.contains(signedInput)) {
            loadTabSnapshot('archived', {
                page: 1,
                searchTerm: state.currentSnapshot.searchTerm,
                signedOnly: signedInput.checked
            });
        }
    }

    contentHost.addEventListener('click', handleClick);
    contentHost.addEventListener('dblclick', handleDoubleClick);
    contentHost.addEventListener('submit', handleSubmit);
    contentHost.addEventListener('change', handleChange);

    window.addEventListener('popstate', () => {
        const entry = window.history.state?.tab
            ? buildSurveyUserHistoryEntry(window.history.state.tab)
            : getSurveyUserHistoryEntryFromLocation(window.location.pathname);

        if (!entry) {
            return;
        }

        handleTabChange(entry.tab, { historyMode: 'none' });
    });

    syncHistory(state.activeTab, 'replace');
    mountSnapshot(initialSnapshot);
};

function getSurveyUserBootstrapData() {
    const bootstrapElement = document.getElementById('survey-user-list-bootstrap')
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

const surveyUserBootstrapData = getSurveyUserBootstrapData();
if (document.querySelector('[data-page="user-surveys"]') && surveyUserBootstrapData) {
    window.bindSurveyUserListPage(surveyUserBootstrapData);
}
