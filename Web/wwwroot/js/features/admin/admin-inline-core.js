import {
    buildRenderableFragment,
    captureInitialDetachedContent,
    hydrateFetchedContentState,
    loadScriptsFromDocument,
    loadStylesheetsFromDocument,
    parseHtmlDocument,
    syncDetachedContent
} from './admin-inline-page-loader.js';
import {
    buildAdminHistoryEntry,
    getAdminHistoryEntryFromLocation,
    normalizeLocationUrl,
    normalizeLogsHistoryId,
    normalizePathname
} from './admin-inline-history.js';
import {
    createAdminModalRenderer,
    createClosedAdminModalState
} from './admin-inline-modal-renderer.js';
import { createAdminOrganizationActions } from './admin-organization-actions.js';
import { createAdminSurveyActions } from './admin-survey-actions.js';
import { resolveAdminTabPageRequest } from './admin-inline-tab-registry.js';
import { createAdminUserActions } from './admin-user-actions.js';

(() => {
    function getRequestVerificationToken() {
        return window.AppHttp?.getAntiforgeryToken() || '';
    }

    function createContentWrapper() {
        const wrapper = document.createElement('div');
        wrapper.className = 'content-wrapper';
        return wrapper;
    }

    const rootElement = document.getElementById('root');
    const existingHeaderHost = document.getElementById('chrome-header');
    const existingNavHost = document.getElementById('chrome-navigation');
    const existingContentAdmin = document.getElementById('content_admin');
    const existingFooterHost = document.getElementById('chrome-footer');
    const layoutContextNode = document.getElementById('layout-chrome-context');
    const hasExistingShell = Boolean(existingHeaderHost && existingNavHost && existingContentAdmin && existingFooterHost);

    if (!rootElement && !hasExistingShell) {
        return;
    }

    captureInitialDetachedContent();

    const initialData = {
        userRole: layoutContextNode?.dataset?.userRole || '',
        userId: Number(layoutContextNode?.dataset?.userId || 0),
        displayName: layoutContextNode?.dataset?.displayName || '',
        userName: layoutContextNode?.dataset?.userName || '',
        organizationName: layoutContextNode?.dataset?.organizationName || '',
        ...(window.__adminBootstrap || {})
    };

    const initialHistoryEntry = getAdminHistoryEntryFromLocation(window.location.pathname, window.location.search)
        || buildAdminHistoryEntry('get_surveys');
    const userRole = initialData.userRole || '';
    const hasAccess = Boolean(userRole);
    const getExtensionModalMount = () => window.AdminInlineAppPages?.mountExtensionModal || null;

    if (!hasAccess) {
        if (!rootElement) {
            return;
        }

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
        modal: createClosedAdminModalState()
    };

    let contentCleanup = null;
    let headerCleanup = null;
    let navCleanup = null;
    let footerCleanup = null;
    let loaderTimer = null;
    let initTogglesTimer = null;
    let initEditTimer = null;
    let contentLifecycleScope = null;

    let pageContainer = rootElement ? document.createElement('div') : (existingHeaderHost.closest('.page-container') || document.body);
    let headerHost = existingHeaderHost;
    let navHost = existingNavHost;
    let contentAdmin = existingContentAdmin;
    let footerHost = existingFooterHost;

    if (rootElement) {
        rootElement.innerHTML = '';
        pageContainer.className = 'page-container';
        headerHost = document.createElement('div');
        const adminContainer = document.createElement('div');
        adminContainer.className = 'admin-container';
        navHost = document.createElement('div');
        contentAdmin = document.createElement('div');
        contentAdmin.id = 'content_admin';
        footerHost = document.createElement('div');
        adminContainer.appendChild(navHost);
        adminContainer.appendChild(contentAdmin);
        pageContainer.appendChild(headerHost);
        pageContainer.appendChild(adminContainer);
        pageContainer.appendChild(footerHost);
        rootElement.appendChild(pageContainer);
    }

    const adminModalRenderer = createAdminModalRenderer({
        pageContainer,
        getExtensionModalMount,
        onClose: () => closeModal(),
        onCopySurvey: () => handleCopySurvey(),
        onUpdateSurvey: () => handleUpdateSurvey(),
        onDeleteSurvey: () => handleDeleteSurvey(),
        onCreateMonthlyReport: (surveyId) => createMonthlyReport(surveyId),
        onCreateMonthlySummaryReport: () => createMonthlySummaryReport(),
        onCreateQuarterlyReport: (quarter) => createQuarterlyReport(quarter)
    });

    const syncBrowserHistory = (historyEntry, mode = 'push') => {
        if (!historyEntry) {
            return;
        }

        const nextState = {
            tab: historyEntry.tab,
            id: historyEntry.id ?? null
        };
        const currentUrl = normalizeLocationUrl(window.location.pathname, window.location.search);

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

    const remountNavigation = () => {
        if (typeof navCleanup === 'function') {
            navCleanup();
        }

        navCleanup = typeof window.mountNavigation === 'function'
            ? window.mountNavigation(navHost, {
                openTab,
                activeTab: state.activeTab,
                userRole: initialData.userRole,
                userId: initialData.userId
            })
            : null;
    };

    const remountChrome = () => {
        if (typeof headerCleanup === 'function') {
            headerCleanup();
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
        remountNavigation();
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
        state.showLoader = false;
        renderLoader();
    };

    const renderLoader = () => {
        const existing = contentAdmin.querySelector('.loading-overlay');
        if (existing) {
            existing.remove();
        }
    };

    const closeModal = () => {
        state.modal = createClosedAdminModalState();
        renderModal();
    };

    const setModal = (nextModal) => {
        state.modal = nextModal;
        renderModal();
    };

    const schedulePostContentHooks = () => {
        const mountedPage = contentAdmin.querySelector('.app-page[data-page]')?.dataset.page || '';
        const schedule = (callback) => {
            if (contentLifecycleScope) {
                contentLifecycleScope.timeout(callback, 0);
                return;
            }

            window.setTimeout(callback, 0);
        };

        if (initTogglesTimer) {
            window.clearTimeout(initTogglesTimer);
        }
        const initializePasswordToggles = () => {
            if (window.initPasswordToggles) {
                window.initPasswordToggles(document);
            }
        };
        if (contentLifecycleScope) {
            contentLifecycleScope.timeout(initializePasswordToggles, 0);
        } else {
            initTogglesTimer = window.setTimeout(initializePasswordToggles, 0);
        }

        if (initEditTimer) {
            window.clearTimeout(initEditTimer);
            initEditTimer = null;
        }
        if (state.activeTab === 'update_survey') {
            const initializeSurveyEdit = () => {
                if (typeof window.surveyEditInit === 'function') {
                    window.surveyEditInit();
                }
            };
            if (contentLifecycleScope) {
                contentLifecycleScope.timeout(initializeSurveyEdit, 0);
            } else {
                initEditTimer = window.setTimeout(initializeSurveyEdit, 0);
            }
        }

        if (mountedPage === 'answers-statistics') {
            schedule(() => {
                if (typeof window.initAnswerStatisticsPage === 'function') {
                    window.initAnswerStatisticsPage();
                }
            });
        }

        if (mountedPage === 'mail-settings-page' || mountedPage === 'mail-compose') {
            schedule(() => {
                if (typeof window.initEmailSettingsPage === 'function') {
                    window.initEmailSettingsPage();
                }
            });
        }

        // Theme, help, and survey auto-creation pages are mounted through AppPageLifecycle.
    };

    const setContentMount = (mountFn) => {
        if (
            contentAdmin.querySelector('.app-page[data-page="theme-settings-page"]')
            && typeof window.teardownThemeSettingsPage === 'function'
        ) {
            window.teardownThemeSettingsPage();
        }
        if (typeof contentCleanup === 'function') {
            contentCleanup();
            contentCleanup = null;
        }
        window.AppPageLifecycle?.unmount(contentAdmin);
        contentLifecycleScope?.dispose();
        contentLifecycleScope = window.AppPageLifecycle?.createScope?.() || null;
        contentAdmin.innerHTML = '';
        const wrapper = createContentWrapper();
        contentAdmin.appendChild(wrapper);
        if (typeof mountFn === 'function') {
            contentCleanup = mountFn(wrapper) || null;
        }
        window.AppPageLifecycle?.mount(contentAdmin);
        schedulePostContentHooks();
        renderLoader();
    };

    const setHtmlContent = (parsedDocument) => {
        const fragment = buildRenderableFragment(parsedDocument);
        setContentMount((host) => {
            host.appendChild(fragment);
            return null;
        });
        syncDetachedContent(parsedDocument);
        hydrateFetchedContentState();
    };

    const fetchHtmlPage = async (endpoint, options) => {
        const response = await fetch(endpoint, {
            ...options,
            cache: 'no-store',
            headers: {
                ...(options?.headers || {}),
                'X-Admin-Inline-Request': 'true'
            }
        });
        if (!response.ok) {
            throw new Error(
                window.getResponseErrorMessage
                    ? window.getResponseErrorMessage(response, 'Ошибка загрузки')
                    : `Ошибка загрузки: ${response.status}`
            );
        }
        const html = await response.text();
        const parsedDocument = parseHtmlDocument(html);
        const nextChromeContext = typeof window.syncAdminChromeContextFromDocument === 'function'
            ? window.syncAdminChromeContextFromDocument(parsedDocument)
            : null;
        if (nextChromeContext && typeof nextChromeContext === 'object') {
            Object.assign(initialData, nextChromeContext);
        }
        loadStylesheetsFromDocument(parsedDocument);
        setHtmlContent(parsedDocument);
        const loadedAnyScript = await loadScriptsFromDocument(parsedDocument);
        if (loadedAnyScript) {
            schedulePostContentHooks();
        }
        return response;
    };

    const renderModal = () => adminModalRenderer.render(state.modal);

    const setActiveTabAndRefreshNav = (tab) => {
        state.activeTab = tab;
        remountNavigation();
    };

    const tryOpenModal = (modalId, openHandler) => {
        const modal = document.getElementById(modalId);
        if (!modal || typeof openHandler !== 'function') {
            return false;
        }

        openHandler();
        return true;
    };

    const openModalWhenReady = (modalId, openHandler) => {
        if (tryOpenModal(modalId, openHandler)) {
            return;
        }

        let attempts = 0;
        const timer = window.setInterval(() => {
            attempts += 1;
            if (tryOpenModal(modalId, openHandler) || attempts >= 20) {
                window.clearInterval(timer);
            }
        }, 50);
    };

    const actionDependencies = {
        fetchPage: fetchHtmlPage,
        getActiveTab: () => state.activeTab,
        getModalData: () => state.modal.data,
        getRequestVerificationToken,
        openModalWhenReady,
        setActiveTab: setActiveTabAndRefreshNav
    };
    const surveyActions = createAdminSurveyActions({
        ...actionDependencies,
        notify: (...args) => window.AppUi?.notify?.(...args)
    });
    const userActions = createAdminUserActions({
        ...actionDependencies,
        notify: (...args) => window.AppUi?.notify?.(...args)
    });
    const organizationActions = createAdminOrganizationActions(actionDependencies);

    function scrollToSelector(selector) {
        if (!selector) {
            return false;
        }

        const target = document.querySelector(selector);
        if (!target) {
            return false;
        }

        target.scrollIntoView({
            block: 'start',
            behavior: 'auto'
        });

        return true;
    }

    function buildListRequestUrl(pathname, queryId = null) {
        const normalizedPath = normalizePathname(pathname);
        const normalizedQuery = normalizeLogsHistoryId(queryId);
        return normalizedQuery
            ? `${normalizedPath}?${normalizedQuery}`
            : normalizedPath;
    }

    const openTab = async (tab, id = undefined, options = {}) => {
        const historyMode = options.historyMode ?? 'push';
        const force = options.force === true;
        const scrollMode = options.scrollMode ?? 'restore';
        const scrollTargetSelector = String(options.scrollTargetSelector || '').trim();
        const historyEntry = buildAdminHistoryEntry(tab, id, state.modal.data);
        const resolvedId = historyEntry?.id ?? id ?? null;

        if (!force && state.activeTab === tab && resolvedId === (window.history.state?.id ?? null)) {
            return;
        }

        if (scrollMode === 'carry') {
            window.AppScrollState?.prepareNavigation({ carry: true });
        } else {
            window.AppScrollState?.saveCurrentPosition();
        }

        const initialPageRequest = tab === 'get_surveys'
            ? resolveAdminTabPageRequest(tab, resolvedId, buildListRequestUrl)
            : null;
        if (initialPageRequest) {
            await fetchHtmlPage(initialPageRequest.url);
            setActiveTabAndRefreshNav(initialPageRequest.activeTab);
            if (historyMode !== 'none') {
                syncBrowserHistory(historyEntry, historyMode);
            }
            if (!scrollToSelector(scrollTargetSelector)) {
                window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === 'carry' });
            }
            return;
        }

        setLoading(true);

        try {
            const pageRequest = resolveAdminTabPageRequest(tab, resolvedId, buildListRequestUrl);
            if (pageRequest) {
                await fetchHtmlPage(pageRequest.url);
                setActiveTabAndRefreshNav(pageRequest.activeTab);
            } else {
                switch (tab) {
                case 'add_survey':
                    await surveyActions.add();
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
                case 'copy_survey':
                    await surveyActions.copy(resolvedId);
                    break;
                case 'update_survey':
                    await surveyActions.edit(resolvedId);
                    break;
                case 'update_archived_survey':
                    await surveyActions.edit(resolvedId, { archived: true });
                    break;
                case 'delete_survey':
                    await surveyActions.removeCurrentSurvey();
                    break;
                case 'add_user':
                    await userActions.add();
                    break;
                case 'update_user':
                    await userActions.edit(resolvedId);
                    break;
                case 'delete_user':
                    await userActions.removeCurrentUser();
                    break;
                case 'add_organization':
                    await organizationActions.add();
                    break;
                case 'update_organization':
                    await organizationActions.edit(resolvedId);
                    break;
                case 'delete_organization':
                    await organizationActions.removeCurrentOrganization();
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
                default:
                    console.warn(`Вкладка ${tab} не обработана.`);
                    break;
                }
            }

            if (historyMode !== 'none') {
                const nextHistory = ['delete_survey'].includes(tab)
                    ? buildAdminHistoryEntry('get_surveys')
                    : ['add_survey', 'update_survey'].includes(tab)
                        ? buildAdminHistoryEntry('get_surveys')
                    : ['update_archived_survey'].includes(tab)
                        ? buildAdminHistoryEntry('archived_surveys')
                    : ['add_user'].includes(tab)
                        ? buildAdminHistoryEntry('get_users')
                    : ['add_organization'].includes(tab)
                        ? buildAdminHistoryEntry('get_organization')
                    : ['delete_user'].includes(tab)
                        ? buildAdminHistoryEntry('get_users')
                        : ['delete_organization'].includes(tab)
                            ? buildAdminHistoryEntry('get_organization')
                            : ['monthly_summary_report', 'quarterly_report_q1', 'quarterly_report_q2', 'quarterly_report_q3', 'quarterly_report_q4'].includes(tab)
                                ? buildAdminHistoryEntry('reports')
                                : historyEntry;
                syncBrowserHistory(nextHistory, ['delete_survey', 'delete_user', 'delete_organization'].includes(tab) ? 'replace' : historyMode);
            }

            if (tab !== 'download_logs') {
                if (!scrollToSelector(scrollTargetSelector)) {
                    window.AppScrollState?.restoreCurrentPosition({ preferCarry: scrollMode === 'carry' });
                }
            }
        } catch (error) {
            console.error('Ошибка переключения вкладки:', error);
            window.AppUi?.notify?.(error.message || 'Произошла ошибка загрузки.', 'error');
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
            await surveyActions.removeCurrentSurvey();
        } catch (error) {
            console.error('Ошибка при удалении анкеты:', error);
            window.AppUi?.notify?.(error.message || 'Не удалось удалить анкету.', 'error');
        } finally {
            setLoading(false);
        }
    };

    remountChrome();
    renderLoader();
    renderModal();

    window.handleTabClick = (tabName, options = {}) => {
        const resolvedOptions = options && typeof options === 'object' ? options : {};
        return openTab(tabName, null, { scrollMode: 'carry', ...resolvedOptions });
    };

    window.refreshAdminTab = (tabName, id = undefined, options = {}) => {
        const resolvedOptions = options && typeof options === 'object' ? options : {};
        return openTab(tabName, id, { force: true, scrollMode: 'restore', ...resolvedOptions });
    };

    document.addEventListener('click', (event) => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const navLink = event.target.closest('.admin-nav .nav-link, .admin-nav .submenu-link');
        if (!navLink) {
            return;
        }

        const tabHolder = navLink.closest('.submenu-item, .nav-item');
        const tabName = tabHolder?.dataset?.tab || '';
        const ownerNavItem = navLink.closest('.nav-item.has-submenu');
        const ownerTab = ownerNavItem?.dataset?.tab || '';
        if (!tabName) {
            return;
        }

        const closeOpenSubmenus = () => {
            document.querySelectorAll('.admin-nav .nav-item.has-submenu.submenu-open').forEach((item) => {
                item.classList.remove('submenu-open');
            });
        };
        const suppressSubmenus = () => {
            if (typeof window.suppressNavigationSubmenus === 'function') {
                window.suppressNavigationSubmenus(document, ownerTab);
                return;
            }

            closeOpenSubmenus();
        };
        const releaseSubmenuSuppression = () => {
            if (typeof window.releaseNavigationSubmenuSuppression === 'function') {
                window.releaseNavigationSubmenuSuppression();
            }
        };
        const isDirectNavDisabled = tabHolder?.classList?.contains('nav-item')
            && tabHolder.classList.contains('has-submenu')
            && tabHolder.dataset.disableDirectNav === 'true';
        const isMobileNavigationViewport = typeof window.isAppMobileNavigationViewport === 'function'
            ? window.isAppMobileNavigationViewport()
            : (
                typeof window.matchMedia === 'function'
                    ? window.matchMedia('(max-width: 900px)').matches || document.body.classList.contains('compact-nav-mode')
                    : window.innerWidth <= 900 || document.body.classList.contains('compact-nav-mode')
            );
        const isMobileSubmenuToggle = isMobileNavigationViewport
            && tabHolder?.classList?.contains('nav-item')
            && tabHolder.classList.contains('has-submenu');

        if (isDirectNavDisabled || isMobileSubmenuToggle) {
            releaseSubmenuSuppression();
            const shouldOpen = !tabHolder.classList.contains('submenu-open');
            closeOpenSubmenus();

            event.preventDefault();
            event.stopPropagation();

            if (shouldOpen) {
                tabHolder.classList.add('submenu-open');
            }
            return;
        }

        suppressSubmenus();

        event.preventDefault();
        event.stopPropagation();
        if (isMobileNavigationViewport && typeof window.closeMobileNavigation === 'function') {
            window.closeMobileNavigation();
        }
        openTab(tabName, null, { scrollMode: 'carry' });
    }, true);

    document.addEventListener('click', (event) => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const link = event.target.closest('a[href]');
        if (!link || link.target || link.hasAttribute('download')) {
            return;
        }

        let targetUrl;
        try {
            targetUrl = new URL(link.href, window.location.href);
        } catch (error) {
            return;
        }

        if (targetUrl.origin !== window.location.origin) {
            return;
        }

        const nextHistoryEntry = getAdminHistoryEntryFromLocation(targetUrl.pathname, targetUrl.search);
        if (!nextHistoryEntry) {
            return;
        }

        event.preventDefault();
        const scrollTargetSelector = link.dataset.scrollTargetSelector || '';
        openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
            scrollMode: scrollTargetSelector ? 'restore' : 'carry',
            scrollTargetSelector
        });
    });

    syncBrowserHistory(initialHistoryEntry, 'replace');
    window.addEventListener('popstate', () => {
        const nextHistoryEntry = window.history.state?.tab
            ? buildAdminHistoryEntry(window.history.state.tab, window.history.state.id)
            : getAdminHistoryEntryFromLocation(window.location.pathname, window.location.search);
        if (nextHistoryEntry) {
            openTab(nextHistoryEntry.tab, nextHistoryEntry.id, {
                historyMode: 'none',
                force: true,
                scrollMode: 'restore'
            });
        }
    });

    if (!rootElement) {
        hydrateFetchedContentState();
        schedulePostContentHooks();
        window.setTimeout(() => {
            if (initialHistoryEntry?.tab === 'add_survey' || initialHistoryEntry?.tab === 'update_survey') {
                setActiveTabAndRefreshNav('get_surveys');
                syncBrowserHistory(buildAdminHistoryEntry('get_surveys'), 'replace');
            } else if (initialHistoryEntry?.tab === 'update_archived_survey') {
                setActiveTabAndRefreshNav('archived_surveys');
                syncBrowserHistory(buildAdminHistoryEntry('archived_surveys'), 'replace');
            } else if (initialHistoryEntry?.tab === 'add_user') {
                setActiveTabAndRefreshNav('get_users');
                syncBrowserHistory(buildAdminHistoryEntry('get_users'), 'replace');
            } else if (initialHistoryEntry?.tab === 'add_organization') {
                setActiveTabAndRefreshNav('get_organization');
                syncBrowserHistory(buildAdminHistoryEntry('get_organization'), 'replace');
            }

            remountChrome();
        }, 0);
        return;
    }

    if (initialHistoryEntry?.tab && initialHistoryEntry.tab !== 'get_surveys') {
        window.setTimeout(() => {
            openTab(initialHistoryEntry.tab, initialHistoryEntry.id, {
                historyMode: 'replace',
                force: true,
                scrollMode: 'restore'
            });
        }, 0);
        return;
    }

    openTab('get_surveys', initialHistoryEntry?.id ?? null, { historyMode: 'replace', force: true, scrollMode: 'restore' });
})();
