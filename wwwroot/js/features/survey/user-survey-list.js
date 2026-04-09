const mountSurveyFillPage = window.mountSurveyFillPage;
const mountCheckAnswersPage = window.mountCheckAnswersPage;

function normalizeSurveyUserPathname(pathname) {
    if (!pathname) {
        return '/';
    }

    return pathname.length > 1 && pathname.endsWith('/')
        ? pathname.slice(0, -1)
        : pathname;
}

function buildSurveyUserHistoryEntry(tab) {
    switch (tab) {
        case 'active':
            return { tab: 'active', url: '/my-surveys' };
        case 'archived':
        case 'archived_surveys_for_user':
            return { tab: 'archived', url: '/my-surveys/archive' };
        case 'help':
            return { tab: 'help', url: '/help' };
        default:
            return null;
    }
}

function getSurveyUserHistoryEntryFromLocation(pathname) {
    const normalizedPath = normalizeSurveyUserPathname(pathname);

    if (normalizedPath === '/my-surveys') {
        return buildSurveyUserHistoryEntry('active');
    }
    if (normalizedPath === '/my-surveys/archive') {
        return buildSurveyUserHistoryEntry('archived');
    }
    if (normalizedPath === '/help') {
        return buildSurveyUserHistoryEntry('help');
    }

    return null;
}

function formatDate(dateString) {
    if (!dateString) {
        return 'Не указано';
    }
    try {
        const date = new Date(dateString);
        return Number.isNaN(date.getTime()) ? 'Не указано' : date.toLocaleDateString('ru-RU');
    } catch {
        return 'Не указано';
    }
}

function computeTimeLeft(dateClose) {
    if (!dateClose) {
        return 'завершено';
    }

    const now = new Date();
    const closeDate = new Date(dateClose);
    const diff = closeDate - now;

    if (diff <= 0) {
        return 'завершено';
    }

    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
    return `${days}д ${hours}ч`;
}

window.renderSurveyUserList = function (initialData) {
    const root = document.getElementById('root');
    const pageTemplate = document.getElementById('survey-user-page-template');
    const cardTemplate = document.getElementById('survey-user-card-template');
    const emptyTemplate = document.getElementById('survey-user-empty-template');
    if (!root || !pageTemplate?.content?.firstElementChild || !cardTemplate?.content?.firstElementChild) {
        return;
    }

    const initialTab = initialData.initialTab === 'archived' ? 'archived' : 'active';
    const initialHistory = getSurveyUserHistoryEntryFromLocation(window.location.pathname)
        || buildSurveyUserHistoryEntry(initialTab)
        || buildSurveyUserHistoryEntry('active');

    const state = {
        activeTab: initialHistory?.tab || initialTab,
        surveys: initialData.initialSurveys || [],
        currentPage: initialData.initialPage || 1,
        totalPages: initialData.initialTotalPages || 1,
        totalCount: initialData.initialTotalCount || 0,
        activeCount: initialTab === 'active' ? (initialData.initialTotalCount || 0) : 0,
        archivedCount: initialTab === 'archived' ? (initialData.initialTotalCount || 0) : 0,
        searchTerm: initialData.initialSearchTerm || '',
        dateFilter: '',
        filterSigned: false,
        loading: false,
        error: null,
        currentView: 'survey-list',
        currentSurvey: null
    };

    root.innerHTML = '';
    const page = pageTemplate.content.firstElementChild.cloneNode(true);
    root.appendChild(page);

    const refs = {
        title: page.querySelector('[data-role="title"]'),
        subtitle: page.querySelector('[data-role="subtitle"]'),
        tabActive: page.querySelector('[data-role="tab-active"]'),
        tabArchived: page.querySelector('[data-role="tab-archived"]'),
        activeCount: page.querySelector('[data-role="active-count"]'),
        errorWrap: page.querySelector('[data-role="error"]'),
        errorText: page.querySelector('[data-role="error-text"]'),
        searchForm: page.querySelector('[data-role="search-form"]'),
        searchInput: page.querySelector('[data-role="search-input"]'),
        monthFilter: page.querySelector('[data-role="month-filter"]'),
        yearFilter: page.querySelector('[data-role="year-filter"]'),
        signedWrap: page.querySelector('[data-role="signed-filter-wrap"]'),
        signedInput: page.querySelector('[data-role="signed-filter-input"]'),
        loading: page.querySelector('[data-role="loading"]'),
        grid: page.querySelector('[data-role="survey-grid"]'),
        pagination: page.querySelector('[data-role="pagination"]'),
        prevPage: page.querySelector('[data-role="prev-page"]'),
        nextPage: page.querySelector('[data-role="next-page"]'),
        pageLabel: page.querySelector('[data-role="page-label"]'),
        fillModalHost: page.querySelector('[data-role="fill-modal-host"]'),
        answersModalHost: page.querySelector('[data-role="answers-modal-host"]')
    };

    const modalState = {
        fillCleanup: null,
        answersCleanup: null
    };

    function renderChrome() {
        const headerHost = page.querySelector('[data-component="header"]');
        const navHost = page.querySelector('[data-component="navigation"]');
        const footerHost = page.querySelector('[data-component="footer"]');

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
                activeTab: state.activeTab,
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

    function mountModal(host, { title, className, onClose, mountBody }) {
        const template = document.getElementById('survey-user-modal-template');
        if (!host || !template?.content?.firstElementChild) {
            return null;
        }

        host.innerHTML = '';
        const modalNode = template.content.firstElementChild.cloneNode(true);
        if (className) {
            modalNode.classList.add(...String(className).split(' ').filter(Boolean));
        }

        const titleWrap = modalNode.querySelector('[data-role="title-wrap"]');
        const titleNode = modalNode.querySelector('[data-role="title"]');
        const modalContent = modalNode.querySelector('.modal-content');
        const closeButton = modalNode.querySelector('[data-role="close-btn"]');
        const bodyHost = modalNode.querySelector('[data-role="body"]');

        if (title && titleWrap && titleNode) {
            titleWrap.style.display = '';
            titleNode.textContent = title;
        }

        const handleEscape = (event) => {
            if (event.key === 'Escape') {
                onClose?.();
            }
        };

        modalNode.addEventListener('click', () => onClose?.());
        modalContent?.addEventListener('click', (event) => event.stopPropagation());
        closeButton?.addEventListener('click', () => onClose?.());
        host.appendChild(modalNode);
        document.body.classList.add('modal-open');
        document.addEventListener('keydown', handleEscape);

        const bodyCleanup = typeof mountBody === 'function' && bodyHost
            ? mountBody(bodyHost)
            : null;

        return () => {
            if (typeof bodyCleanup === 'function') {
                bodyCleanup();
            }
            document.body.classList.remove('modal-open');
            document.removeEventListener('keydown', handleEscape);
            host.innerHTML = '';
        };
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

    function filteredSurveys() {
        let result = state.surveys;

        if (state.dateFilter) {
            const filterDate = new Date(state.dateFilter);
            if (!Number.isNaN(filterDate.getTime())) {
                result = result.filter((survey) => {
                    if (state.activeTab === 'active') {
                        const startDate = new Date(survey.date_open);
                        const endDate = new Date(survey.date_close);
                        endDate.setHours(23, 59, 59, 999);
                        return filterDate >= startDate && filterDate <= endDate;
                    }
                    return survey.completion_date
                        ? new Date(survey.completion_date).toDateString() === filterDate.toDateString()
                        : false;
                });
            }
        }

        if (state.activeTab === 'archived' && state.filterSigned) {
            result = result.filter((survey) => survey.csp);
        }

        return result;
    }

    function renderModals() {
        cleanupModal('fill');
        cleanupModal('answers');

        if (state.currentView === 'survey-fill' && state.currentSurvey && refs.fillModalHost) {
            modalState.fillCleanup = mountModal(refs.fillModalHost, {
                title: 'Активная анкета',
                onClose: handleBackToList,
                mountBody: (modalBodyHost) => (typeof mountSurveyFillPage === 'function'
                    ? mountSurveyFillPage(modalBodyHost, {
                        survey: state.currentSurvey,
                        organizationId: initialData.userOrganizationId,
                        userRole: initialData.userRole,
                        onBack: handleBackToList
                    })
                    : null)
            });
        }

        if (state.currentView === 'check-answers' && state.currentSurvey && refs.answersModalHost) {
            modalState.answersCleanup = mountModal(refs.answersModalHost, {
                title: 'Ответы на анкету',
                onClose: handleBackToList,
                mountBody: (modalBodyHost) => (typeof mountCheckAnswersPage === 'function'
                    ? mountCheckAnswersPage(modalBodyHost, {
                        survey: state.currentSurvey,
                        organizationId: initialData.userOrganizationId,
                        userRole: initialData.userRole,
                        onBack: handleBackToList
                    })
                    : null)
            });
        }
    }

    function renderCards() {
        refs.grid.innerHTML = '';
        const list = filteredSurveys();

        if (list.length === 0) {
            if (emptyTemplate?.content?.firstElementChild) {
                refs.grid.appendChild(emptyTemplate.content.firstElementChild.cloneNode(true));
            }
            return;
        }

        list.forEach((survey) => {
            const card = cardTemplate.content.firstElementChild.cloneNode(true);
            card.querySelector('[data-role="name"]').textContent = survey.name_survey || 'Без названия';
            card.querySelector('[data-role="description"]').textContent = survey.description || 'Нет описания';
            card.querySelector('[data-role="period"]').textContent = `${formatDate(survey.date_open)} - ${formatDate(survey.date_close)}`;

            const completionInfo = card.querySelector('[data-role="completion-info"]');
            const activeInfo = card.querySelector('[data-role="active-info"]');
            const signature = card.querySelector('[data-role="signature"]');
            const signatureStatus = card.querySelector('[data-role="signature-status"]');
            const status = card.querySelector('[data-role="status"]');

            if (state.activeTab === 'archived') {
                completionInfo.style.display = '';
                activeInfo.style.display = 'none';
                completionInfo.querySelector('[data-role="completion-text"]').textContent = `Заполнено: ${formatDate(survey.completion_date)}`;
                signature.style.display = '';
                signatureStatus.textContent = survey.csp ? 'подписано' : 'не подписано';
                signatureStatus.classList.toggle('signed', Boolean(survey.csp));
                signatureStatus.classList.toggle('not-signed', !survey.csp);
                status.textContent = 'Завершена';
                status.className = 'status archived';
            } else {
                completionInfo.style.display = 'none';
                activeInfo.style.display = '';
                activeInfo.querySelector('.time-left').textContent = computeTimeLeft(survey.date_close);
                signature.style.display = 'none';
                status.textContent = 'Активна';
                status.className = 'status active';
            }

            card.querySelector('[data-role="card-click"]').addEventListener('click', () => {
                state.currentSurvey = survey;
                state.currentView = state.activeTab === 'active' ? 'survey-fill' : 'check-answers';
                renderModals();
            });

            refs.grid.appendChild(card);
        });
    }

    function render() {
        refs.title.textContent = state.activeTab === 'active' ? 'Доступные анкеты' : 'Пройденные анкеты';
        refs.subtitle.textContent = state.activeTab === 'active'
            ? 'Ниже вы можете ознакомиться с доступными вам анкетами'
            : 'Ниже вы можете ознакомиться с пройденными вами анкетами';

        refs.tabActive.classList.toggle('active-tab', state.activeTab === 'active');
        refs.tabArchived.classList.toggle('active-tab', state.activeTab === 'archived');
        refs.activeCount.textContent = String(state.activeCount);

        refs.errorWrap.style.display = state.error ? 'flex' : 'none';
        refs.errorText.textContent = state.error || '';

        refs.searchInput.value = state.searchTerm;
        refs.signedWrap.style.display = state.activeTab === 'archived' ? '' : 'none';
        refs.signedInput.checked = state.filterSigned;

        refs.loading.style.display = state.loading ? '' : 'none';
        refs.grid.style.display = state.loading ? 'none' : '';

        const showPagination = state.activeTab === 'active' && filteredSurveys().length > 0;
        refs.pagination.style.display = showPagination ? 'flex' : 'none';
        refs.prevPage.disabled = state.currentPage <= 1;
        refs.nextPage.disabled = state.currentPage >= state.totalPages;
        refs.pageLabel.textContent = `Страница ${state.currentPage} из ${state.totalPages}`;

        renderCards();
        renderModals();
    }

    async function loadCounts() {
        try {
            const activeResponse = await fetch('/my-surveys?page=1&searchTerm=', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (activeResponse.ok) {
                const activeData = await activeResponse.json();
                state.activeCount = activeData.totalCount || activeData.accessibleSurveys?.length || 0;
            }

            const archiveResponse = await fetch(`/my-surveys/archive/${initialData.userId}?searchTerm=`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (archiveResponse.ok) {
                const archiveData = await archiveResponse.json();
                state.archivedCount = archiveData.totalCount || archiveData.accessibleSurveys?.length || 0;
            }
        } catch (error) {
            console.error('Ошибка загрузки счетчиков:', error);
        }
    }

    async function loadSurveyData(tab, pageNumber, search, signedOnly, date) {
        state.loading = true;
        state.error = null;
        render();
        try {
            const endpoint = tab === 'active'
                ? `/my-surveys?page=${pageNumber}&searchTerm=${search}&date=${date}`
                : `/my-surveys/archive/${initialData.userId}?searchTerm=${search}&signedOnly=${signedOnly}&date=${date}`;
            const response = await fetch(endpoint, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) {
                throw new Error('Ошибка загрузки данных анкет');
            }
            const data = await response.json();
            state.surveys = data.accessibleSurveys || [];
            if (tab === 'active') {
                state.currentPage = data.currentPage || 1;
                state.totalPages = data.totalPages || 1;
                state.activeCount = data.totalCount || state.activeCount;
            } else {
                state.currentPage = 1;
                state.totalPages = 1;
                state.archivedCount = data.totalCount || state.archivedCount;
            }
        } catch (error) {
            state.error = error.message || 'Ошибка загрузки';
        } finally {
            state.loading = false;
            render();
        }
    }

    function handleTabChange(tab, options = {}) {
        if (tab === 'help') {
            window.location.href = '/help';
            return;
        }

        const normalized = tab === 'archived_surveys_for_user' ? 'archived' : tab;
        if (normalized !== 'active' && normalized !== 'archived') {
            return;
        }

        state.activeTab = normalized;
        state.currentPage = 1;
        state.currentView = 'survey-list';
        state.currentSurvey = null;

        if (options.historyMode !== 'none') {
            syncHistory(normalized, options.historyMode || 'push');
        }

        loadSurveyData(state.activeTab, 1, state.searchTerm, state.filterSigned, state.dateFilter);
    }

    function handleBackToList() {
        state.currentView = 'survey-list';
        state.currentSurvey = null;
        renderModals();
    }

    refs.tabActive.addEventListener('click', () => handleTabChange('active'));
    refs.tabArchived.addEventListener('click', () => handleTabChange('archived'));
    refs.searchForm.addEventListener('submit', (event) => {
        event.preventDefault();
        loadSurveyData(state.activeTab, 1, state.searchTerm, state.filterSigned, state.dateFilter);
    });
    refs.searchInput.addEventListener('input', (event) => {
        state.searchTerm = event.target.value;
    });
    refs.signedInput.addEventListener('change', (event) => {
        state.filterSigned = event.target.checked;
        if (state.activeTab === 'archived') {
            loadSurveyData('archived', 1, state.searchTerm, state.filterSigned, state.dateFilter);
        }
    });
    refs.monthFilter.addEventListener('click', () => window.populateMonthOptions && window.populateMonthOptions());
    refs.monthFilter.addEventListener('change', () => {
        state.dateFilter = refs.monthFilter.value ? `${new Date().getFullYear()}-${refs.monthFilter.value}-01` : '';
        loadSurveyData(state.activeTab, state.currentPage, state.searchTerm, state.filterSigned, state.dateFilter);
    });
    refs.yearFilter.addEventListener('click', () => window.populateYearOptions && window.populateYearOptions());
    refs.yearFilter.addEventListener('change', () => window.filterByDate && window.filterByDate());
    refs.prevPage.addEventListener('click', () => {
        if (state.currentPage > 1) {
            loadSurveyData(state.activeTab, state.currentPage - 1, state.searchTerm, state.filterSigned, state.dateFilter);
        }
    });
    refs.nextPage.addEventListener('click', () => {
        if (state.currentPage < state.totalPages) {
            loadSurveyData(state.activeTab, state.currentPage + 1, state.searchTerm, state.filterSigned, state.dateFilter);
        }
    });

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
    renderChrome();
    render();
    loadCounts().then(render);
    loadSurveyData(state.activeTab, state.currentPage, state.searchTerm, state.filterSigned, state.dateFilter);
};

function getSurveyUserBootstrapData() {
    const bootstrapElement = document.getElementById('survey-user-list-bootstrap')
        || document.getElementById('user-archive-bootstrap');
    if (!bootstrapElement?.content?.textContent) {
        return null;
    }
    try {
        return JSON.parse(bootstrapElement.content.textContent.trim());
    } catch (error) {
        console.error('Не удалось прочитать bootstrap-данные user survey:', error);
        return null;
    }
}

const surveyUserBootstrapData = getSurveyUserBootstrapData();
if (document.getElementById('root') && surveyUserBootstrapData) {
    window.renderSurveyUserList(surveyUserBootstrapData);
}
