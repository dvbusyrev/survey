(() => {
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

    function createContentWrapper() {
        const wrapper = document.createElement('div');
        wrapper.className = 'content-wrapper';
        return wrapper;
    }

    const rootElement = document.getElementById('root');
    if (!rootElement) {
        return;
    }

    const initialData = window.__adminBootstrap || {};
    const initialHistoryEntry = getAdminHistoryEntryFromLocation(window.location.pathname) || buildAdminHistoryEntry('get_surveys');
    const userRole = initialData.userRole || '';
    const hasAccess = Boolean(userRole);
    const availablePages = window.AdminInlineAppPages || {};
    const mountExtensionModal = availablePages.mountExtensionModal;
    const mountStatisticsPage = availablePages.mountStatisticsPage;

    if (!hasAccess) {
        rootElement.innerHTML = '';
        const denied = document.createElement('div');
        denied.className = 'access-denied';
        const h2 = document.createElement('h2');
        h2.textContent = 'Доступ запрещён';
        const p = document.createElement('p');
        p.textContent = 'У вас нет прав для просмотра этой страницы.';
        const br = document.createElement('br');
        const a = document.createElement('a');
        a.href = '/';
        a.className = 'btn';
        a.textContent = 'Вернуться на страницу авторизации';
        denied.appendChild(h2);
        denied.appendChild(p);
        denied.appendChild(br);
        denied.appendChild(a);
        rootElement.appendChild(denied);
        return;
    }

    const state = {
        activeTab: initialHistoryEntry?.tab || 'get_surveys',
        loading: false,
        showLoader: false,
        modal: createClosedModalState()
    };

    let contentCleanup = null;
    let modalCleanup = null;
    let headerCleanup = null;
    let navCleanup = null;
    let footerCleanup = null;
    let loaderTimer = null;
    let initTogglesTimer = null;
    let initEditTimer = null;

    rootElement.innerHTML = '';
    const pageContainer = document.createElement('div');
    pageContainer.className = 'page-container';
    const headerHost = document.createElement('div');
    const adminContainer = document.createElement('div');
    adminContainer.className = 'admin-container';
    const navHost = document.createElement('div');
    const contentAdmin = document.createElement('div');
    contentAdmin.id = 'content_admin';
    const footerHost = document.createElement('div');
    const modalNode = document.createElement('div');
    const modalContent = document.createElement('div');
    modalContent.className = 'modal-content';
    const modalClose = document.createElement('span');
    modalClose.className = 'modal-close';
    const modalIcon = document.createElement('i');
    modalIcon.className = 'fas fa-xmark';
    const modalBodyHost = document.createElement('div');
    modalClose.appendChild(modalIcon);
    modalContent.appendChild(modalClose);
    modalContent.appendChild(modalBodyHost);
    modalNode.appendChild(modalContent);
    adminContainer.appendChild(navHost);
    adminContainer.appendChild(contentAdmin);
    pageContainer.appendChild(headerHost);
    pageContainer.appendChild(adminContainer);
    pageContainer.appendChild(footerHost);
    pageContainer.appendChild(modalNode);
    rootElement.appendChild(pageContainer);

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

    const remountChrome = () => {
        if (typeof headerCleanup === 'function') {
            headerCleanup();
        }
        if (typeof navCleanup === 'function') {
            navCleanup();
        }
        if (typeof footerCleanup === 'function') {
            footerCleanup();
        }
        headerCleanup = typeof window.mountHeader === 'function'
            ? window.mountHeader(headerHost, {
                userRole: initialData.userRole,
                displayName: initialData.displayName,
                userName: initialData.userName,
                organizationName: initialData.organizationName
            })
            : null;
        navCleanup = typeof window.mountNavigation === 'function'
            ? window.mountNavigation(navHost, {
                openTab,
                activeTab: state.activeTab,
                userRole: initialData.userRole,
                userId: initialData.userId
            })
            : null;
        footerCleanup = typeof window.mountFooter === 'function'
            ? window.mountFooter(footerHost)
            : null;
    };

    const setLoading = (isLoading) => {
        state.loading = isLoading;
        if (loaderTimer) {
            window.clearTimeout(loaderTimer);
            loaderTimer = null;
        }
        if (isLoading) {
            loaderTimer = window.setTimeout(() => {
                state.showLoader = true;
                renderLoader();
            }, 180);
        } else {
            state.showLoader = false;
            renderLoader();
        }
    };

    const renderLoader = () => {
        const existing = contentAdmin.querySelector('.loading-overlay');
        if (state.showLoader) {
            if (!existing) {
                const overlay = document.createElement('div');
                overlay.className = 'loading-overlay';
                const text = document.createElement('div');
                text.textContent = 'Загрузка...';
                overlay.appendChild(text);
                contentAdmin.appendChild(overlay);
            }
        } else if (existing) {
            existing.remove();
        }
    };

    const closeModal = () => {
        state.modal = createClosedModalState();
        renderModal();
    };

    const setModal = (nextModal) => {
        state.modal = nextModal;
        renderModal();
    };

    const schedulePostContentHooks = () => {
        if (initTogglesTimer) {
            window.clearTimeout(initTogglesTimer);
        }
        initTogglesTimer = window.setTimeout(() => {
            if (window.initPasswordToggles) {
                window.initPasswordToggles(document);
            }
        }, 0);

        if (initEditTimer) {
            window.clearTimeout(initEditTimer);
            initEditTimer = null;
        }
        if (state.activeTab === 'update_survey') {
            initEditTimer = window.setTimeout(() => {
                if (typeof window.surveyEditInit === 'function') {
                    window.surveyEditInit();
                }
            }, 0);
        }
    };

    const setContentMount = (mountFn) => {
        if (typeof contentCleanup === 'function') {
            contentCleanup();
            contentCleanup = null;
        }
        contentAdmin.innerHTML = '';
        const wrapper = createContentWrapper();
        contentAdmin.appendChild(wrapper);
        if (typeof mountFn === 'function') {
            contentCleanup = mountFn(wrapper) || null;
        }
        schedulePostContentHooks();
        renderLoader();
    };

    const setHtmlContent = (html) => {
        const parsedHtml = extractRenderableHtml(html);
        const parser = new DOMParser();
        const parsedDocument = parser.parseFromString(parsedHtml, 'text/html');
        const fragment = document.createDocumentFragment();
        Array.from(parsedDocument.body.childNodes).forEach((node) => {
            fragment.appendChild(node.cloneNode(true));
        });
        setContentMount((host) => {
            host.appendChild(fragment);
            return null;
        });
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
        return result;
    };

    const renderModal = () => {
        modalNode.className = `modal ${state.modal.isOpen ? 'modal--visible' : ''}`;
        if (typeof modalCleanup === 'function') {
            modalCleanup();
            modalCleanup = null;
        }
        modalBodyHost.innerHTML = '';
        if (!state.modal.isOpen) {
            return;
        }

        const modalData = state.modal.data;
        switch (state.modal.content) {
            case 'extend':
                if (typeof mountExtensionModal === 'function') {
                    modalCleanup = mountExtensionModal(modalBodyHost, { survey: modalData, onClose: closeModal }) || null;
                } else {
                    const msg = document.createElement('div');
                    msg.textContent = 'Модуль продления не загружен.';
                    modalBodyHost.appendChild(msg);
                }
                return;
            case 'report': {
                const wrap = document.createElement('div');
                const title = document.createElement('h2');
                title.className = 'modal-title';
                title.textContent = 'Сформировать отчёт';
                wrap.appendChild(title);
                const actions = document.createElement('div');
                actions.style.display = 'flex';
                actions.style.gap = '10px';
                actions.style.justifyContent = 'space-between';
                actions.style.marginTop = '1.5rem';
                const month = document.createElement('div');
                month.className = 'submenu2-container';
                month.style.flex = '1';
                const monthBtn = document.createElement('button');
                monthBtn.style.width = '100%';
                monthBtn.textContent = 'Отчёт за месяц';
                const monthMenu = document.createElement('div');
                monthMenu.className = 'submenu2';
                const bySurvey = document.createElement('div');
                bySurvey.textContent = 'По выбранной анкете';
                bySurvey.addEventListener('click', () => createMonthlyReport(modalData?.id_survey));
                const allSurveys = document.createElement('div');
                allSurveys.textContent = 'По всем анкетам';
                allSurveys.addEventListener('click', () => createMonthlySummaryReport());
                monthMenu.appendChild(bySurvey);
                monthMenu.appendChild(allSurveys);
                month.appendChild(monthBtn);
                month.appendChild(monthMenu);
                const quarter = document.createElement('div');
                quarter.className = 'submenu2-container';
                quarter.style.flex = '1';
                const quarterBtn = document.createElement('button');
                quarterBtn.style.width = '100%';
                quarterBtn.textContent = 'Отчёт за квартал';
                const quarterMenu = document.createElement('div');
                quarterMenu.className = 'submenu2';
                [1, 2, 3, 4].forEach((q) => {
                    const item = document.createElement('div');
                    item.textContent = `${q} квартал`;
                    item.addEventListener('click', () => createQuarterlyReport(q));
                    quarterMenu.appendChild(item);
                });
                quarter.appendChild(quarterBtn);
                quarter.appendChild(quarterMenu);
                actions.appendChild(month);
                actions.appendChild(quarter);
                wrap.appendChild(actions);
                modalBodyHost.appendChild(wrap);
                return;
            }
            case 'copy':
            case 'update':
            case 'delete': {
                const isCopy = state.modal.content === 'copy';
                const isUpdate = state.modal.content === 'update';
                const titleText = isCopy ? 'Копирование анкеты' : isUpdate ? 'Редактирование анкеты' : 'Удаление анкеты';
                const messageText = isCopy
                    ? `Вы уверены, что хотите создать копию анкеты "${modalData?.name_survey}"?`
                    : isUpdate
                        ? `Вы переходите к редактированию анкеты "${modalData?.name_survey}".`
                        : `Вы уверены, что хотите удалить анкету "${modalData?.name_survey}"?`;
                const okText = isCopy ? 'Копировать' : isUpdate ? 'Продолжить' : 'Удалить';
                const okHandler = isCopy ? handleCopySurvey : isUpdate ? handleUpdateSurvey : handleDeleteSurvey;
                const root = document.createElement('div');
                const header = document.createElement('div');
                header.className = 'modal-header';
                const h2 = document.createElement('h2');
                h2.className = 'h2_modal';
                h2.textContent = titleText;
                header.appendChild(h2);
                const body = document.createElement('div');
                body.className = 'modal-body';
                const p = document.createElement('p');
                p.className = 'modal-message';
                p.textContent = messageText;
                body.appendChild(p);
                const footer = document.createElement('div');
                footer.className = 'modal-footer';
                const ok = document.createElement('button');
                ok.className = 'modal_btn modal_btn-primary';
                ok.textContent = okText;
                ok.addEventListener('click', okHandler);
                const cancel = document.createElement('button');
                cancel.className = 'modal_btn modal_btn-secondary';
                cancel.textContent = 'Отмена';
                cancel.addEventListener('click', closeModal);
                footer.appendChild(ok);
                footer.appendChild(cancel);
                root.appendChild(header);
                root.appendChild(body);
                root.appendChild(footer);
                modalBodyHost.appendChild(root);
                return;
            }
            case 'message': {
                const root = document.createElement('div');
                const header = document.createElement('div');
                header.className = 'modal-header';
                const h2 = document.createElement('h2');
                h2.className = 'h2_modal';
                h2.textContent = state.modal.isSuccess ? 'Успешно' : 'Ошибка';
                header.appendChild(h2);
                const body = document.createElement('div');
                body.className = 'modal-body';
                const message = document.createElement('div');
                message.className = `modal-message ${state.modal.isSuccess ? 'success-message' : 'error-message'}`;
                message.textContent = state.modal.message || '';
                body.appendChild(message);
                const footer = document.createElement('div');
                footer.className = 'modal-footer';
                const ok = document.createElement('button');
                ok.className = 'modal_btn modal_btn-primary';
                ok.textContent = 'OK';
                ok.addEventListener('click', closeModal);
                footer.appendChild(ok);
                root.appendChild(header);
                root.appendChild(body);
                root.appendChild(footer);
                modalBodyHost.appendChild(root);
                return;
            }
            default:
                return;
        }
    };

    const setActiveTabAndRefreshNav = (tab) => {
        state.activeTab = tab;
        remountChrome();
        schedulePostContentHooks();
    };

    const openTab = async (tab, id = null, options = {}) => {
        const historyMode = options.historyMode ?? 'push';
        const force = options.force === true;
        const historyEntry = buildAdminHistoryEntry(tab, id, state.modal.data);
        const resolvedId = historyEntry?.id ?? id ?? null;

        if (!force && state.activeTab === tab && resolvedId === (window.history.state?.id ?? null)) {
            return;
        }

        if (tab === 'get_surveys') {
            await fetchHtmlPage('/surveys');
            setActiveTabAndRefreshNav(tab);
            if (historyMode !== 'none') {
                syncBrowserHistory(historyEntry, historyMode);
            }
            return;
        }

        setLoading(true);

        try {
            switch (tab) {
                case 'open_statistics':
                    if (typeof mountStatisticsPage !== 'function') {
                        throw new Error('Модуль статистики не загружен.');
                    }
                    setContentMount((host) => mountStatisticsPage(host));
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'list_answers_users':
                    await fetchHtmlPage('/surveys/answers');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'archived_surveys':
                    await fetchHtmlPage('/surveys/archive');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'get_survey_signatures':
                    if (!id) throw new Error('ID анкеты не указан.');
                    await fetchHtmlPage(`/surveys/${id}/signatures`);
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'add_survey':
                    await fetchHtmlPage('/surveys/create');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'get_logs':
                    await fetchHtmlPage('/logs');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'download_logs': {
                    const response = await fetch('/logs/export');
                    if (!response.ok) {
                        throw new Error(window.getResponseErrorMessage
                            ? window.getResponseErrorMessage(response, 'Ошибка выгрузки логов')
                            : `Ошибка выгрузки логов: ${response.status}`);
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
                    break;
                }
                case 'get_users':
                    await fetchHtmlPage('/users');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'get_organization':
                    await fetchHtmlPage('/organizations');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'copy_survey':
                    if (!resolvedId) throw new Error('ID анкеты не указан.');
                    await fetchHtmlPage(`/surveys/${resolvedId}/copy`);
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'update_survey':
                    if (!resolvedId) throw new Error('ID анкеты не указан.');
                    await fetchHtmlPage(`/surveys/${resolvedId}/edit`);
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'delete_survey': {
                    const result = await deleteSurvey(state.modal.data?.id_survey);
                    setModal({ isOpen: true, content: 'message', message: result.message, isSuccess: true, data: null });
                    setActiveTabAndRefreshNav('get_surveys');
                    break;
                }
                case 'add_user':
                    await fetchHtmlPage('/users/create');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'update_user':
                    if (!resolvedId) throw new Error('ID пользователя не указан.');
                    await fetchHtmlPage(`/users/${resolvedId}/edit`);
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'delete_user':
                    await fetchHtmlPage(`/users/${state.modal.data?.id_user}/delete`, {
                        method: 'POST',
                        headers: { RequestVerificationToken: getRequestVerificationToken() }
                    });
                    setActiveTabAndRefreshNav('get_users');
                    break;
                case 'archive_list_organizations':
                    await fetchHtmlPage('/organizations/archive');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'archive_list_users':
                    await fetchHtmlPage('/users/archive');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'add_organization':
                    await fetchHtmlPage('/organizations/create');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'update_organization':
                    if (!resolvedId) throw new Error('ID организации не указан.');
                    await fetchHtmlPage(`/organizations/${resolvedId}/edit`);
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'delete_organization':
                    await fetchHtmlPage(`/organizations/${state.modal.data?.organization_id}/delete`, {
                        method: 'POST',
                        headers: { RequestVerificationToken: getRequestVerificationToken() }
                    });
                    setActiveTabAndRefreshNav('get_organization');
                    break;
                case 'help':
                    window.open('/help_files/admin_survey_guide.docx', '_blank');
                    await fetchHtmlPage('/help');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'monthly_summary_report':
                    createMonthlySummaryReport();
                    await fetchHtmlPage('/reports');
                    setActiveTabAndRefreshNav('reports');
                    break;
                case 'quarterly_report_q1':
                case 'quarterly_report_q2':
                case 'quarterly_report_q3':
                case 'quarterly_report_q4':
                    createQuarterlyReport(Number(tab.slice(-1)));
                    await fetchHtmlPage('/reports');
                    setActiveTabAndRefreshNav('reports');
                    break;
                case 'reports':
                    await fetchHtmlPage('/reports');
                    setActiveTabAndRefreshNav(tab);
                    break;
                case 'email':
                    await fetchHtmlPage('/mail-settings');
                    setActiveTabAndRefreshNav(tab);
                    break;
                default:
                    console.warn(`Вкладка ${tab} не обработана.`);
                    break;
            }

            if (historyMode !== 'none') {
                const nextHistory = ['delete_survey'].includes(tab)
                    ? buildAdminHistoryEntry('get_surveys')
                    : ['delete_user'].includes(tab)
                        ? buildAdminHistoryEntry('get_users')
                        : ['delete_organization'].includes(tab)
                            ? buildAdminHistoryEntry('get_organization')
                            : ['monthly_summary_report', 'quarterly_report_q1', 'quarterly_report_q2', 'quarterly_report_q3', 'quarterly_report_q4'].includes(tab)
                                ? buildAdminHistoryEntry('reports')
                                : historyEntry;
                syncBrowserHistory(nextHistory, ['delete_survey', 'delete_user', 'delete_organization'].includes(tab) ? 'replace' : historyMode);
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
            const result = await deleteSurvey(state.modal.data?.id_survey);
            setModal({
                isOpen: true,
                content: 'message',
                message: result.message,
                isSuccess: true,
                data: null
            });
            setActiveTabAndRefreshNav('get_surveys');
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

    modalClose.addEventListener('click', closeModal);
    remountChrome();
    renderLoader();
    renderModal();

    window.handleTabClick = (tabName) => {
        openTab(tabName);
    };

    syncBrowserHistory(initialHistoryEntry, 'replace');
    window.addEventListener('popstate', () => {
        const nextHistoryEntry = window.history.state?.tab
            ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id)
            : getAdminHistoryEntryFromLocation(window.location.pathname);
        if (nextHistoryEntry) {
            openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
                historyMode: 'none',
                force: true
            });
        }
    });

    if (initialHistoryEntry?.tab && initialHistoryEntry.tab !== 'get_surveys') {
        window.setTimeout(() => {
            openTab(initialHistoryEntry.tab, initialHistoryEntry.id, {
                historyMode: 'replace',
                force: true
            });
        }, 0);
    } else {
        openTab('get_surveys', null, { historyMode: 'replace', force: true });
    }
})();
