(() => {
    const { Header, Footer, Navigation } = window;
    const ADMIN_MENU_TABS = ['get_surveys', 'list_answers_users', 'archived_surveys'];

    function normalizePathname(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    function buildAdminHistoryEntry(tab, id = null, modalData = null) {
        const surveyId = id ?? modalData?.id_survey ?? null;
        const userId = id ?? modalData?.id_user ?? null;
        const organizationId = id ?? modalData?.organization_id ?? null;

        switch (tab) {
            case 'get_surveys':
                return { tab, id: null, url: '/surveys' };
            case 'list_answers_users':
                return { tab, id: null, url: '/surveys/answers' };
            case 'archived_surveys':
                return { tab, id: null, url: '/surveys/archive' };
            case 'get_survey_signatures':
                return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/signatures` } : null;
            case 'add_survey':
                return { tab, id: null, url: '/surveys/create' };
            case 'copy_survey':
                return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/copy` } : null;
            case 'update_survey':
                return surveyId ? { tab, id: surveyId, url: `/surveys/${surveyId}/edit` } : null;
            case 'open_statistics':
                return { tab, id: null, url: '/statistics' };
            case 'get_users':
                return { tab, id: null, url: '/users' };
            case 'add_user':
                return { tab, id: null, url: '/users/create' };
            case 'update_user':
                return userId ? { tab, id: userId, url: `/users/${userId}/edit` } : null;
            case 'archive_list_users':
                return { tab, id: null, url: '/users/archive' };
            case 'get_organization':
                return { tab, id: null, url: '/organizations' };
            case 'add_organization':
                return { tab, id: null, url: '/organizations/create' };
            case 'update_organization':
                return organizationId ? { tab, id: organizationId, url: `/organizations/${organizationId}/edit` } : null;
            case 'archive_list_organizations':
                return { tab, id: null, url: '/organizations/archive' };
            case 'reports':
                return { tab, id: null, url: '/reports' };
            case 'get_logs':
                return { tab, id: null, url: '/logs' };
            case 'email':
                return { tab, id: null, url: '/mail-settings' };
            case 'help':
                return { tab, id: null, url: '/help' };
            default:
                return null;
        }
    }

    function getAdminHistoryEntryFromLocation(pathname) {
        const normalizedPath = normalizePathname(pathname);

        if (normalizedPath === '/surveys') {
            return buildAdminHistoryEntry('get_surveys');
        }

        if (normalizedPath === '/surveys/answers') {
            return buildAdminHistoryEntry('list_answers_users');
        }

        if (normalizedPath === '/surveys/archive') {
            return buildAdminHistoryEntry('archived_surveys');
        }

        if (normalizedPath === '/surveys/create') {
            return buildAdminHistoryEntry('add_survey');
        }

        if (normalizedPath === '/statistics') {
            return buildAdminHistoryEntry('open_statistics');
        }

        if (normalizedPath === '/users') {
            return buildAdminHistoryEntry('get_users');
        }

        if (normalizedPath === '/users/create') {
            return buildAdminHistoryEntry('add_user');
        }

        if (normalizedPath === '/users/archive') {
            return buildAdminHistoryEntry('archive_list_users');
        }

        if (normalizedPath === '/organizations') {
            return buildAdminHistoryEntry('get_organization');
        }

        if (normalizedPath === '/organizations/create') {
            return buildAdminHistoryEntry('add_organization');
        }

        if (normalizedPath === '/organizations/archive') {
            return buildAdminHistoryEntry('archive_list_organizations');
        }

        if (normalizedPath === '/reports') {
            return buildAdminHistoryEntry('reports');
        }

        if (normalizedPath === '/logs') {
            return buildAdminHistoryEntry('get_logs');
        }

        if (normalizedPath === '/mail-settings') {
            return buildAdminHistoryEntry('email');
        }

        if (normalizedPath === '/help') {
            return buildAdminHistoryEntry('help');
        }

        let match = normalizedPath.match(/^\/surveys\/(\d+)\/signatures$/);
        if (match) {
            return buildAdminHistoryEntry('get_survey_signatures', Number(match[1]));
        }

        match = normalizedPath.match(/^\/surveys\/(\d+)\/edit$/);
        if (match) {
            return buildAdminHistoryEntry('update_survey', Number(match[1]));
        }

        match = normalizedPath.match(/^\/surveys\/(\d+)\/copy$/);
        if (match) {
            return buildAdminHistoryEntry('copy_survey', Number(match[1]));
        }

        match = normalizedPath.match(/^\/users\/(\d+)\/edit$/);
        if (match) {
            return buildAdminHistoryEntry('update_user', Number(match[1]));
        }

        match = normalizedPath.match(/^\/organizations\/(\d+)\/edit$/);
        if (match) {
            return buildAdminHistoryEntry('update_organization', Number(match[1]));
        }

        return null;
    }

    function createClosedModalState() {
        return {
            isOpen: false,
            content: '',
            data: null,
            message: null,
            isSuccess: false
        };
    }

    function getRequestVerificationToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function extractRenderableHtml(html) {
        if (!html) {
            return '';
        }

        try {
            const parser = new DOMParser();
            const documentFragment = parser.parseFromString(html, 'text/html');
            documentFragment.querySelectorAll('link, style, script, meta, title').forEach((node) => node.remove());
            return documentFragment.body && documentFragment.body.innerHTML.trim()
                ? documentFragment.body.innerHTML
                : html;
        } catch (error) {
            console.error('Ошибка парсинга HTML:', error);
            return html;
        }
    }

    function renderContentWrapper(content) {
        return <div className="content-wrapper">{content}</div>;
    }

    function getMonthName(dateValue) {
        const parsedDate = new Date(dateValue);
        if (Number.isNaN(parsedDate.getTime())) {
            return 'Не указано';
        }

        return parsedDate.toLocaleString('ru', { month: 'long' });
    }

    function getOrganizationOptions(surveys) {
        return Array.from(new Set(
            surveys.flatMap((survey) => survey.organization_name
                ? survey.organization_name.split(',').map((name) => name.trim())
                : []
            ).filter(Boolean)
        ));
    }

    function App() {
        const initialData = window.__adminBootstrap || {};
        const initialHistoryEntry = React.useMemo(
            () => getAdminHistoryEntryFromLocation(window.location.pathname) || buildAdminHistoryEntry('get_surveys'),
            []
        );
        const recordsPerPage = 10;
        const [activeTab, setActiveTab] = React.useState(initialHistoryEntry?.tab || 'get_surveys');
        const [content, setContent] = React.useState(null);
        const [loading, setLoading] = React.useState(false);
        const [showLoader, setShowLoader] = React.useState(false);
        const [monthFilter, setMonthFilter] = React.useState('');
        const [organizationFilter, setOrganizationFilter] = React.useState('');
        const [currentPage, setCurrentPage] = React.useState(1);
        const [modal, setModal] = React.useState(createClosedModalState);
        const [searchTerm, setSearchTerm] = React.useState('');
        const [surveys, setSurveys] = React.useState(() => Array.isArray(initialData.surveys) ? initialData.surveys : []);

        const userRole = initialData.userRole || '';
        const hasAccess = Boolean(userRole);

        const openTabRef = React.useRef(null);

        const availablePages = window.AdminInlineAppPages || {};
        const ExtensionModal = availablePages.ExtensionModal;
        const StatisticsPage = availablePages.StatisticsPage;

        const syncBrowserHistory = (historyEntry, mode = 'push') => {
            if (!historyEntry) {
                return;
            }

            const nextState = {
                tab: historyEntry.tab,
                id: historyEntry.id ?? null
            };
            const currentUrl = normalizePathname(window.location.pathname);

            if (mode === 'replace') {
                window.history.replaceState(nextState, '', historyEntry.url);
                return;
            }

            if (currentUrl === historyEntry.url
                && window.history.state?.tab === nextState.tab
                && (window.history.state?.id ?? null) === nextState.id) {
                return;
            }

            window.history.pushState(nextState, '', historyEntry.url);
        };

        const closeModal = () => {
            setModal(createClosedModalState());
        };

        const filterSurveys = (surveyItems) => {
            return surveyItems.filter((survey) => {
                const searchValue = searchTerm.toLowerCase();
                const nameSurvey = String(survey.name_survey || '').toLowerCase();
                const organizationName = String(survey.organization_name || '').toLowerCase();

                const matchesSearch = searchValue === ''
                    || nameSurvey.includes(searchValue)
                    || organizationName.includes(searchValue);

                const matchesMonth = monthFilter === ''
                    || (new Date(survey.date_open).getMonth() + 1).toString() === monthFilter;

                const matchesOrganization = organizationFilter === ''
                    || String(survey.organization_name || '')
                        .split(',')
                        .some((name) => name.trim().toLowerCase() === organizationFilter.toLowerCase());

                return matchesSearch && matchesMonth && matchesOrganization;
            });
        };

        const getCurrentRecords = () => {
            const filteredSurveys = filterSurveys(surveys);
            const indexOfLastRecord = currentPage * recordsPerPage;
            const indexOfFirstRecord = indexOfLastRecord - recordsPerPage;

            return {
                filteredSurveys,
                currentRecords: filteredSurveys.slice(indexOfFirstRecord, indexOfLastRecord),
                totalPages: Math.max(1, Math.ceil(filteredSurveys.length / recordsPerPage)),
                totalRecords: filteredSurveys.length
            };
        };

        const renderMenuAndFilters = () => (
            <div className="menu-bar">
                <div
                    className={`menu-tab ${activeTab === 'get_surveys' ? 'active-tab' : ''}`}
                    onClick={() => openTabRef.current?.('get_surveys')}
                >
                    Анкеты
                </div>
                <div
                    className={`menu-tab ${activeTab === 'list_answers_users' ? 'active-tab' : ''}`}
                    onClick={() => openTabRef.current?.('list_answers_users')}
                >
                    Ответы
                </div>
                <div
                    className={`menu-tab ${activeTab === 'archived_surveys' ? 'active-tab' : ''}`}
                    onClick={() => openTabRef.current?.('archived_surveys')}
                >
                    Архив
                </div>
            </div>
        );

        const renderSurveys = () => {
            const { currentRecords, totalPages, totalRecords } = getCurrentRecords();
            const organizationOptions = getOrganizationOptions(surveys);

            return (
                <div className="content surveys-page" id="default_content" data-page="surveys-list">
                    <div className="note">
                        <h2>Список существующих анкет</h2>
                        <p>
                            На данной странице представлен перечень анкет. В таблице вы можете
                            редактировать, удалять, формировать отчёты, проверять подписи и копировать анкеты.
                        </p>
                    </div>

                    <div className="filter-sort">
                        <button id="add_survey_btn" onClick={() => openTabRef.current?.('add_survey')}>
                            Добавить анкету
                        </button>
                        <input
                            type="text"
                            placeholder="Поиск..."
                            value={searchTerm}
                            onChange={(event) => {
                                setSearchTerm(event.target.value);
                                setCurrentPage(1);
                            }}
                        />
                        <select
                            value={monthFilter}
                            onChange={(event) => {
                                setMonthFilter(event.target.value);
                                setCurrentPage(1);
                            }}
                            className="filter-select"
                        >
                            <option value="">Все месяцы</option>
                            <option value="1">Январь</option>
                            <option value="2">Февраль</option>
                            <option value="3">Март</option>
                            <option value="4">Апрель</option>
                            <option value="5">Май</option>
                            <option value="6">Июнь</option>
                            <option value="7">Июль</option>
                            <option value="8">Август</option>
                            <option value="9">Сентябрь</option>
                            <option value="10">Октябрь</option>
                            <option value="11">Ноябрь</option>
                            <option value="12">Декабрь</option>
                        </select>
                        <select
                            value={organizationFilter}
                            onChange={(event) => {
                                setOrganizationFilter(event.target.value);
                                setCurrentPage(1);
                            }}
                            className="filter-select"
                        >
                            <option value="">Все организации</option>
                            {organizationOptions.map((organization, index) => (
                                <option key={index} value={organization}>
                                    {organization}
                                </option>
                            ))}
                        </select>
                    </div>

                    <table id="data_table" className="surveys-table">
                        <thead>
                            <tr className="table_tr">
                                <th className="table-th--start">Название анкеты</th>
                                <th>Месяц</th>
                                <th>Организация</th>
                                <th className="table-th--end th-centered">Действия</th>
                            </tr>
                        </thead>
                        <tbody>
                            {totalRecords === 0 ? (
                                <tr className="no-results-row">
                                    <td colSpan="4">
                                        <div className="no-results-message">
                                            <i className="fas fa-search"></i>
                                            <span>Анкеты не найдены</span>
                                        </div>
                                    </td>
                                </tr>
                            ) : (
                                currentRecords.map((survey) => (
                                    <tr key={survey.id_survey}>
                                        <td>{survey.name_survey}</td>
                                        <td>{getMonthName(survey.date_open)}</td>
                                        <td>
                                            {survey.organization_name
                                                ? survey.organization_name
                                                    .split(',')
                                                    .map((name) => name.trim())
                                                    .filter(Boolean)
                                                    .join(', ')
                                                : 'Не указано'}
                                        </td>
                                        <td className="action-icons">
                                            <div className="icon-container" onClick={() => setModal({ isOpen: true, content: 'report', data: survey })}>
                                                <span><i className="fas fa-list-check"></i></span>
                                                <span className="icon-tooltip">Сформировать отчёт</span>
                                            </div>
                                            <div className="icon-container" onClick={() => openTabRef.current?.('get_survey_signatures', survey.id_survey)}>
                                                <span><i className="fas fa-check"></i></span>
                                                <span className="icon-tooltip">Проверить подпись</span>
                                            </div>
                                            <div className="icon-container" onClick={() => setModal({ isOpen: true, content: 'extend', data: survey })}>
                                                <span><i className="fas fa-hourglass-half"></i></span>
                                                <span className="icon-tooltip">Продлить</span>
                                            </div>
                                            <div className="icon-container" onClick={() => setModal({ isOpen: true, content: 'copy', data: survey })}>
                                                <span><i className="fas fa-clipboard-copy"></i></span>
                                                <span className="icon-tooltip">Копировать</span>
                                            </div>
                                            <div className="icon-container" onClick={() => setModal({ isOpen: true, content: 'update', data: survey })}>
                                                <span><i className="fas fa-pen-to-square"></i></span>
                                                <span className="icon-tooltip">Редактировать</span>
                                            </div>
                                            <div className="icon-container" onClick={() => setModal({ isOpen: true, content: 'delete', data: survey })}>
                                                <span><i className="fas fa-trash"></i></span>
                                                <span className="icon-tooltip">Удалить</span>
                                            </div>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>

                    {totalRecords > 0 && (
                        <div className="pagination-container">
                            {currentPage > 1 && (
                                <button
                                    className="pagination-button prev"
                                    onClick={() => setCurrentPage((page) => page - 1)}
                                >
                                    <i className="fas fa-chevron-left"></i> Предыдущая
                                </button>
                            )}

                            <div className="page-info">
                                Страница {Math.min(currentPage, totalPages)} из {totalPages}
                            </div>

                            {currentPage < totalPages && (
                                <button
                                    className="pagination-button next"
                                    onClick={() => setCurrentPage((page) => page + 1)}
                                >
                                    Следующая <i className="fas fa-chevron-right"></i>
                                </button>
                            )}
                        </div>
                    )}
                </div>
            );
        };

        const setHtmlContent = (html) => {
            setContent(renderContentWrapper(<div dangerouslySetInnerHTML={{ __html: extractRenderableHtml(html) }} />));
        };

        const fetchHtmlPage = async (endpoint, options) => {
            const response = await fetch(endpoint, options);
            if (!response.ok) {
                throw new Error(
                    window.getResponseErrorMessage
                        ? window.getResponseErrorMessage(response, 'Ошибка загрузки')
                        : `Ошибка загрузки: ${response.status}`
                );
            }

            const html = await response.text();
            setHtmlContent(html);
            return response;
        };

        const deleteSurvey = async (surveyId) => {
            const response = await fetch(`/surveys/${surveyId}/delete`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getRequestVerificationToken()
                },
                body: JSON.stringify({ surveyId })
            });

            const result = await response.json();
            if (!response.ok) {
                throw new Error(result.message || 'Ошибка при удалении анкеты.');
            }

            if (Array.isArray(result.surveys)) {
                setSurveys(result.surveys);
            }

            return result;
        };

        const openTab = async (tab, id = null, options = {}) => {
            const historyMode = options.historyMode ?? 'push';
            const force = options.force === true;
            const historyEntry = buildAdminHistoryEntry(tab, id, modal.data);
            const resolvedId = historyEntry?.id ?? id ?? null;

            if (!force && activeTab === tab && resolvedId === (window.history.state?.id ?? null)) {
                return;
            }

            if (tab === 'get_surveys') {
                setActiveTab(tab);
                if (historyMode !== 'none') {
                    syncBrowserHistory(historyEntry, historyMode);
                }
                return;
            }

            setLoading(true);

            try {
                switch (tab) {
                    case 'open_statistics':
                        if (typeof StatisticsPage !== 'function') {
                            throw new Error('Модуль статистики не загружен.');
                        }
                        setContent(renderContentWrapper(<StatisticsPage />));
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'list_answers_users':
                        await fetchHtmlPage('/surveys/answers');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'archived_surveys':
                        await fetchHtmlPage('/surveys/archive');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'get_survey_signatures':
                        if (!id) {
                            throw new Error('ID анкеты не указан.');
                        }
                        await fetchHtmlPage(`/surveys/${id}/signatures`);
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'add_survey':
                        await fetchHtmlPage('/surveys/create');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'get_logs':
                        await fetchHtmlPage('/logs');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'download_logs': {
                        const response = await fetch('/logs/export');
                        if (!response.ok) {
                            throw new Error(
                                window.getResponseErrorMessage
                                    ? window.getResponseErrorMessage(response, 'Ошибка выгрузки логов')
                                    : `Ошибка выгрузки логов: ${response.status}`
                            );
                        }

                        const blob = await response.blob();
                        const downloadUrl = window.URL.createObjectURL(blob);
                        const link = document.createElement('a');
                        link.href = downloadUrl;
                        link.download = 'logs.txt';
                        document.body.appendChild(link);
                        link.click();
                        link.remove();
                        window.URL.revokeObjectURL(downloadUrl);
                        return;
                    }

                    case 'get_users':
                        await fetchHtmlPage('/users');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'get_organization':
                        await fetchHtmlPage('/organizations');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'copy_survey':
                        if (!resolvedId) {
                            throw new Error('ID анкеты не указан.');
                        }
                        await fetchHtmlPage(`/surveys/${resolvedId}/copy`);
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'update_survey':
                        if (!resolvedId) {
                            throw new Error('ID анкеты не указан.');
                        }
                        await fetchHtmlPage(`/surveys/${resolvedId}/edit`);
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'delete_survey': {
                        const result = await deleteSurvey(modal.data?.id_survey);
                        setModal({
                            isOpen: true,
                            content: 'message',
                            message: result.message,
                            isSuccess: true,
                            data: null
                        });
                        setActiveTab('get_surveys');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('get_surveys'), 'replace');
                        }
                        return;
                    }

                    case 'add_user':
                        await fetchHtmlPage('/users/create');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'update_user':
                        if (!resolvedId) {
                            throw new Error('ID пользователя не указан.');
                        }
                        await fetchHtmlPage(`/users/${resolvedId}/edit`);
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'delete_user':
                        await fetchHtmlPage(`/users/${modal.data?.id_user}/delete`, {
                            method: 'POST',
                            headers: {
                                RequestVerificationToken: getRequestVerificationToken()
                            }
                        });
                        setActiveTab('get_users');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('get_users'), 'replace');
                        }
                        return;

                    case 'archive_list_organizations':
                        await fetchHtmlPage('/organizations/archive');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'archive_list_users':
                        await fetchHtmlPage('/users/archive');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'add_organization':
                        await fetchHtmlPage('/organizations/create');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'update_organization':
                        if (!resolvedId) {
                            throw new Error('ID организации не указан.');
                        }
                        await fetchHtmlPage(`/organizations/${resolvedId}/edit`);
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'delete_organization':
                        await fetchHtmlPage(`/organizations/${modal.data?.organization_id}/delete`, {
                            method: 'POST',
                            headers: {
                                RequestVerificationToken: getRequestVerificationToken()
                            }
                        });
                        setActiveTab('get_organization');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('get_organization'), 'replace');
                        }
                        return;

                    case 'help':
                        window.open('/help_files/admin_survey_guide.docx', '_blank');
                        await fetchHtmlPage('/help');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'monthly_summary_report':
                        createMonthlySummaryReport();
                        await fetchHtmlPage('/reports');
                        setActiveTab('reports');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('reports'), historyMode);
                        }
                        return;

                    case 'quarterly_report_q1':
                        createQuarterlyReport(1);
                        await fetchHtmlPage('/reports');
                        setActiveTab('reports');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('reports'), historyMode);
                        }
                        return;

                    case 'quarterly_report_q2':
                        createQuarterlyReport(2);
                        await fetchHtmlPage('/reports');
                        setActiveTab('reports');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('reports'), historyMode);
                        }
                        return;

                    case 'quarterly_report_q3':
                        createQuarterlyReport(3);
                        await fetchHtmlPage('/reports');
                        setActiveTab('reports');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('reports'), historyMode);
                        }
                        return;

                    case 'quarterly_report_q4':
                        createQuarterlyReport(4);
                        await fetchHtmlPage('/reports');
                        setActiveTab('reports');
                        if (historyMode !== 'none') {
                            syncBrowserHistory(buildAdminHistoryEntry('reports'), historyMode);
                        }
                        return;

                    case 'reports':
                        await fetchHtmlPage('/reports');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    case 'email':
                        await fetchHtmlPage('/mail-settings');
                        setActiveTab(tab);
                        if (historyMode !== 'none') {
                            syncBrowserHistory(historyEntry, historyMode);
                        }
                        return;

                    default:
                        console.warn(`Вкладка ${tab} не обработана.`);
                        return;
                }
            } catch (error) {
                console.error('Ошибка переключения вкладки:', error);
                setModal({
                    isOpen: true,
                    content: 'message',
                    message: error.message || 'Произошла ошибка загрузки.',
                    isSuccess: false,
                    data: null
                });
            } finally {
                setLoading(false);
            }
        };

        openTabRef.current = openTab;

        const handleCopySurvey = async () => {
            closeModal();
            await openTab('copy_survey');
        };

        const handleUpdateSurvey = async () => {
            closeModal();
            await openTab('update_survey');
        };

        const handleDeleteSurvey = async () => {
            try {
                setLoading(true);
                const result = await deleteSurvey(modal.data?.id_survey);

                setModal({
                    isOpen: true,
                    content: 'message',
                    message: result.message,
                    isSuccess: true,
                    data: null
                });
                setActiveTab('get_surveys');
            } catch (error) {
                console.error('Ошибка при удалении анкеты:', error);
                setModal({
                    isOpen: true,
                    content: 'message',
                    message: error.message || 'Не удалось удалить анкету.',
                    isSuccess: false,
                    data: null
                });
            } finally {
                setLoading(false);
            }
        };

        const renderModalContent = () => {
            switch (modal.content) {
                case 'extend':
                    return typeof ExtensionModal === 'function'
                        ? <ExtensionModal survey={modal.data} onClose={closeModal} />
                        : <div>Модуль продления не загружен.</div>;

                case 'report':
                    return (
                        <div>
                            <h2 className="modal-title">Сформировать отчёт</h2>
                            <div
                                style={{
                                    display: 'flex',
                                    gap: '10px',
                                    justifyContent: 'space-between',
                                    marginTop: '1.5rem'
                                }}
                            >
                                <div className="submenu2-container" style={{ flex: 1 }}>
                                    <button style={{ width: '100%' }}>Отчёт за месяц</button>
                                    <div className="submenu2">
                                        <div onClick={() => createMonthlyReport(modal.data?.id_survey)}>По выбранной анкете</div>
                                        <div onClick={() => createMonthlySummaryReport()}>По всем анкетам</div>
                                    </div>
                                </div>

                                <div className="submenu2-container" style={{ flex: 1 }}>
                                    <button style={{ width: '100%' }}>Отчёт за квартал</button>
                                    <div className="submenu2">
                                        <div onClick={() => createQuarterlyReport(1)}>1 квартал</div>
                                        <div onClick={() => createQuarterlyReport(2)}>2 квартал</div>
                                        <div onClick={() => createQuarterlyReport(3)}>3 квартал</div>
                                        <div onClick={() => createQuarterlyReport(4)}>4 квартал</div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    );

                case 'copy':
                    return (
                        <div>
                            <div className="modal-header">
                                <h2 className="h2_modal">Копирование анкеты</h2>
                            </div>
                            <div className="modal-body">
                                <p className="modal-message">
                                    Вы уверены, что хотите создать копию анкеты "{modal.data?.name_survey}"?
                                </p>
                            </div>
                            <div className="modal-footer">
                                <button className="modal_btn modal_btn-primary" onClick={handleCopySurvey}>Копировать</button>
                                <button className="modal_btn modal_btn-secondary" onClick={closeModal}>Отмена</button>
                            </div>
                        </div>
                    );

                case 'update':
                    return (
                        <div>
                            <div className="modal-header">
                                <h2 className="h2_modal">Редактирование анкеты</h2>
                            </div>
                            <div className="modal-body">
                                <p className="modal-message">
                                    Вы переходите к редактированию анкеты "{modal.data?.name_survey}".
                                </p>
                            </div>
                            <div className="modal-footer">
                                <button className="modal_btn modal_btn-primary" onClick={handleUpdateSurvey}>Продолжить</button>
                                <button className="modal_btn modal_btn-secondary" onClick={closeModal}>Отмена</button>
                            </div>
                        </div>
                    );

                case 'delete':
                    return (
                        <div>
                            <div className="modal-header">
                                <h2 className="h2_modal">Удаление анкеты</h2>
                            </div>
                            <div className="modal-body">
                                <p className="modal-message">
                                    Вы уверены, что хотите удалить анкету "{modal.data?.name_survey}"?
                                </p>
                            </div>
                            <div className="modal-footer">
                                <button className="modal_btn modal_btn-primary" onClick={handleDeleteSurvey}>Удалить</button>
                                <button className="modal_btn modal_btn-secondary" onClick={closeModal}>Отмена</button>
                            </div>
                        </div>
                    );

                case 'message':
                    return (
                        <div>
                            <div className="modal-header">
                                <h2 className="h2_modal">{modal.isSuccess ? 'Успешно' : 'Ошибка'}</h2>
                            </div>
                            <div className="modal-body">
                                <div className={`modal-message ${modal.isSuccess ? 'success-message' : 'error-message'}`}>
                                    {modal.message}
                                </div>
                            </div>
                            <div className="modal-footer">
                                <button className="modal_btn modal_btn-primary" onClick={closeModal}>OK</button>
                            </div>
                        </div>
                    );

                default:
                    return null;
            }
        };

        React.useEffect(() => {
            window.handleTabClick = (tabName) => {
                if (openTabRef.current) {
                    openTabRef.current(tabName);
                }
            };

            return () => {
                delete window.handleTabClick;
            };
        }, []);

        React.useEffect(() => {
            const handlePopState = () => {
                if (!openTabRef.current) {
                    return;
                }

                const nextHistoryEntry = window.history.state?.tab
                    ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id)
                    : getAdminHistoryEntryFromLocation(window.location.pathname);

                if (nextHistoryEntry) {
                    openTabRef.current(nextHistoryEntry.tab, nextHistoryEntry.id, {
                        historyMode: 'none',
                        force: true
                    });
                }
            };

            syncBrowserHistory(initialHistoryEntry, 'replace');
            window.addEventListener('popstate', handlePopState);
            return () => window.removeEventListener('popstate', handlePopState);
        }, []);

        React.useEffect(() => {
            if (!initialHistoryEntry || initialHistoryEntry.tab === 'get_surveys') {
                return;
            }

            const timer = window.setTimeout(() => {
                openTabRef.current?.(initialHistoryEntry.tab, initialHistoryEntry.id, {
                    historyMode: 'replace',
                    force: true
                });
            }, 0);

            return () => window.clearTimeout(timer);
        }, [initialHistoryEntry]);

        React.useEffect(() => {
            const timer = window.setTimeout(() => {
                if (window.initPasswordToggles) {
                    window.initPasswordToggles(document);
                }
            }, 0);

            return () => window.clearTimeout(timer);
        }, [content]);

        React.useEffect(() => {
            if (loading) {
                const timer = window.setTimeout(() => setShowLoader(true), 180);
                return () => window.clearTimeout(timer);
            }

            setShowLoader(false);
        }, [loading]);

        React.useEffect(() => {
            if (activeTab !== 'get_surveys') {
                return;
            }

            setContent(renderContentWrapper(renderSurveys()));
        }, [activeTab, surveys, searchTerm, monthFilter, organizationFilter, currentPage]);

        React.useEffect(() => {
            const filteredSurveyCount = filterSurveys(surveys).length;
            const totalPages = Math.max(1, Math.ceil(filteredSurveyCount / recordsPerPage));
            if (currentPage > totalPages) {
                setCurrentPage(totalPages);
            }
        }, [surveys, searchTerm, monthFilter, organizationFilter, currentPage]);

        React.useEffect(() => {
            if (activeTab !== 'update_survey') {
                return;
            }

            const timer = window.setTimeout(() => {
                if (typeof window.surveyEditInit === 'function') {
                    window.surveyEditInit();
                }
            }, 0);

            return () => window.clearTimeout(timer);
        }, [activeTab, content]);

        if (!hasAccess) {
            return (
                <div className="access-denied">
                    <h2>Доступ запрещён</h2>
                    <p>У вас нет прав для просмотра этой страницы.</p>
                    <br />
                    <a href="/" className="btn">Вернуться на страницу авторизации</a>
                </div>
            );
        }

        return (
            <div className="page-container">
                <Header
                    userRole={initialData.userRole}
                    displayName={initialData.displayName}
                    userName={initialData.userName}
                    organizationName={initialData.organizationName}
                />
                <div className="admin-container">
                    <Navigation
                        openTab={openTab}
                        activeTab={activeTab}
                        userRole={initialData.userRole}
                        userId={initialData.userId}
                    />
                    <div id="content_admin">
                        {showLoader ? (
                            <div className="loading-overlay">
                                <div>Загрузка...</div>
                            </div>
                        ) : (
                            <>
                                {ADMIN_MENU_TABS.includes(activeTab) && renderMenuAndFilters()}
                                {content}
                            </>
                        )}
                    </div>
                </div>
                <Footer />

                <div className={`modal ${modal.isOpen ? 'modal--visible' : ''}`}>
                    <div className="modal-content">
                        <span className="modal-close" onClick={closeModal}>
                            <i className="fas fa-xmark"></i>
                        </span>
                        {renderModalContent()}
                    </div>
                </div>
            </div>
        );
    }

    const rootElement = document.getElementById('root');
    if (rootElement && typeof React !== 'undefined' && typeof ReactDOM !== 'undefined') {
        const root = ReactDOM.createRoot(rootElement);
        root.render(<App />);
    }
})();
