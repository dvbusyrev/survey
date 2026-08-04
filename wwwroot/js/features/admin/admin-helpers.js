(function () {
    function normalizePathname(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    const adminListTabs = new Set([
        'get_surveys',
        'list_answers_users',
        'archived_surveys',
        'get_users',
        'archived_users',
        'get_organization',
        'archive_list_organizations',
        'get_logs'
    ]);

    const adminRoutes = {
        get_surveys: '/survey',
        list_answers_users: '/survey/answer',
        archived_surveys: '/survey/archive',
        add_survey: '/survey/create',
        open_statistics: '/statistics',
        get_users: '/users',
        add_user: '/users/create',
        archived_users: '/users/archive',
        get_organization: '/organizations',
        organization_surveys: '/organizations/survey',
        add_organization: '/organizations/create',
        archive_list_organizations: '/organizations/archive',
        reports: '/reports',
        survey_auto_creation: '/settings/survey-creation',
        theme_settings: '/settings/theme',
        get_logs: '/logs',
        email: '/email',
        email_new: '/email',
        email_settings: '/settings/email',
        help: '/help',
        monthly_summary_report: '/reports',
        quarterly_report_q1: '/reports',
        quarterly_report_q2: '/reports',
        quarterly_report_q3: '/reports',
        quarterly_report_q4: '/reports'
    };

    function normalizeQuery(value) {
        const rawValue = String(value || '').trim();
        if (!rawValue) {
            return '';
        }

        return rawValue.startsWith('?') ? rawValue : `?${rawValue}`;
    }

    function resolveAdminRoute(tabName, id = null) {
        const tab = String(tabName || '').trim();
        if (!tab) {
            return '';
        }

        const hasId = id !== null && id !== undefined && id !== '';
        if (tab === 'update_survey') {
            return hasId ? `/survey/${id}/edit` : '';
        }

        if (tab === 'update_archived_survey') {
            return hasId ? `/survey/archive/${id}/edit` : '';
        }

        if (tab === 'copy_survey') {
            return hasId ? `/survey/${id}/copy` : '';
        }

        if (tab === 'update_user') {
            return hasId ? `/users/${id}/edit` : '';
        }

        if (tab === 'update_organization') {
            return hasId ? `/organizations/${id}/edit` : '';
        }

        if (tab === 'get_survey_signatures') {
            return hasId ? `/survey/${id}/signatures` : '';
        }

        const route = adminRoutes[tab] || '';
        if (!route) {
            return '';
        }

        return adminListTabs.has(tab) && hasId
            ? `${route}${normalizeQuery(id)}`
            : route;
    }

    function navigateAdminUrl(url, options = {}) {
        const resolvedUrl = String(url || '').trim();
        if (!resolvedUrl) {
            return Promise.resolve(false);
        }

        if (options.scrollMode === 'carry') {
            window.AppScrollState?.prepareNavigation?.({ carry: true });
        } else {
            window.AppScrollState?.saveCurrentPosition?.();
        }

        if (options.historyMode === 'replace') {
            window.location.replace(resolvedUrl);
        } else {
            window.location.assign(resolvedUrl);
        }

        return new Promise(() => {});
    }

    function resolveCurrentAdminTab(pathname = window.location.pathname) {
        const normalizedPath = normalizePathname(pathname).toLowerCase();

        if (normalizedPath === '/statistics') {
            return 'open_statistics';
        }

        if (normalizedPath === '/survey/answer' || normalizedPath === '/surveys/answers') {
            return 'list_answers_users';
        }

        if (normalizedPath === '/survey/archive'
            || normalizedPath === '/surveys/archive'
            || /^\/survey\/archive\/\d+\/edit$/.test(normalizedPath)
            || /^\/surveys\/archive\/\d+\/edit$/.test(normalizedPath)) {
            return 'archived_surveys';
        }

        if (normalizedPath === '/survey'
            || normalizedPath === '/surveys'
            || normalizedPath === '/survey/create'
            || normalizedPath === '/surveys/create'
            || /^\/survey\/\d+\/edit$/.test(normalizedPath)
            || /^\/surveys\/\d+\/edit$/.test(normalizedPath)
            || /^\/survey\/\d+\/copy$/.test(normalizedPath)
            || /^\/surveys\/\d+\/copy$/.test(normalizedPath)) {
            return 'get_surveys';
        }

        if (normalizedPath === '/users/archive') {
            return 'archived_users';
        }

        if (normalizedPath === '/users'
            || normalizedPath === '/users/create'
            || /^\/users\/\d+\/edit$/.test(normalizedPath)) {
            return 'get_users';
        }

        if (normalizedPath === '/organizations/archive') {
            return 'archive_list_organizations';
        }

        if (normalizedPath === '/organizations/survey'
            || normalizedPath === '/organizations/surveys') {
            return 'organization_surveys';
        }

        if (normalizedPath === '/organizations'
            || normalizedPath === '/organizations/create'
            || /^\/organizations\/\d+\/edit$/.test(normalizedPath)) {
            return 'get_organization';
        }

        if (normalizedPath === '/reports') {
            return 'reports';
        }

        if (normalizedPath === '/settings/survey-creation' || normalizedPath === '/survey-auto-creation') {
            return 'survey_auto_creation';
        }

        if (normalizedPath === '/settings/theme'
            || normalizedPath === '/theme/configuration'
            || normalizedPath === '/theme-settings') {
            return 'theme_settings';
        }

        if (normalizedPath === '/logs' || normalizedPath === '/event-log') {
            return 'get_logs';
        }

        if (normalizedPath === '/email'
            || normalizedPath === '/mail'
            || normalizedPath === '/mail/new') {
            return 'email_new';
        }

        if (normalizedPath === '/settings/email'
            || normalizedPath === '/mail/configuration'
            || normalizedPath === '/mail-settings') {
            return 'email_settings';
        }

        if (normalizedPath === '/help') {
            return 'help';
        }

        return null;
    }

    function readChromeContext(contextNode) {
        if (!contextNode?.dataset) {
            return null;
        }

        return {
            userRole: contextNode.dataset.userRole || '',
            userId: Number(contextNode.dataset.userId || 0),
            displayName: contextNode.dataset.displayName || '',
            userName: contextNode.dataset.userName || '',
            organizationName: contextNode.dataset.organizationName || ''
        };
    }

    function applyChromeContext(nextContext) {
        if (!nextContext || typeof nextContext !== 'object') {
            return null;
        }

        let contextNode = document.getElementById('layout-chrome-context');
        if (!contextNode) {
            contextNode = document.createElement('div');
            contextNode.id = 'layout-chrome-context';
            contextNode.hidden = true;
            document.body.appendChild(contextNode);
        }

        contextNode.dataset.userRole = nextContext.userRole || '';
        contextNode.dataset.userId = String(nextContext.userId || 0);
        contextNode.dataset.displayName = nextContext.displayName || '';
        contextNode.dataset.userName = nextContext.userName || '';
        contextNode.dataset.organizationName = nextContext.organizationName || '';

        window.__adminChromeContext = {
            ...(window.__adminChromeContext && typeof window.__adminChromeContext === 'object'
                ? window.__adminChromeContext
                : {}),
            ...nextContext
        };

        return window.__adminChromeContext;
    }

    function syncAdminChromeContextFromDocument(sourceDocument) {
        if (!sourceDocument?.getElementById) {
            return null;
        }

        const nextContext = readChromeContext(sourceDocument.getElementById('layout-chrome-context'));
        return applyChromeContext(nextContext);
    }

    function getHttpStatusMessage(status, statusText) {
        switch (Number(status)) {
            case 0:
                return 'Сервер недоступен или соединение прервано.';
            case 400:
                return 'Некорректный запрос.';
            case 401:
                return 'Требуется авторизация.';
            case 403:
                return 'Доступ запрещён.';
            case 404:
                return 'Страница не найдена.';
            case 409:
                return 'Конфликт данных.';
            case 422:
                return 'Данные не прошли проверку.';
            case 500:
                return 'Произошла внутренняя ошибка сервера.';
            case 502:
            case 503:
            case 504:
                return 'Сервер временно недоступен.';
            default:
                return statusText && String(statusText).trim()
                    ? String(statusText).trim()
                    : `Ошибка сервера (${status})`;
        }
    }

    function getResponseErrorMessage(response, prefix) {
        const resolvedPrefix = prefix || 'Ошибка';
        return `${resolvedPrefix}: ${getHttpStatusMessage(response?.status, response?.statusText)}`;
    }

    function handleResponse(response) {
        if (!response.ok) {
            throw new Error(getResponseErrorMessage(response, 'Ошибка запроса'));
        }
        return response.json();
    }

    function handleError(error) {
        console.error('Ошибка:', error);
        window.AppUi?.notify?.(
            'Произошла ошибка: ' + (error.message || 'Неизвестная ошибка'),
            'error'
        );
    }

    function getValueSafe(elementId) {
        const element = document.getElementById(elementId);
        return element ? element.value : '';
    }

    function refreshAdminUi({ tabName, id = undefined, fallbackUrl, options } = {}) {
        const resolvedTabName = tabName || resolveCurrentAdminTab();
        const resolvedOptions = options && typeof options === 'object'
            ? options
            : {};
        const resolvedUrl = fallbackUrl || resolveAdminRoute(resolvedTabName, id) || window.location.pathname;
        return navigateAdminUrl(resolvedUrl, {
            historyMode: 'replace',
            scrollMode: 'carry',
            ...resolvedOptions
        });
    }

    function handleAdminMutationSuccess({ message, notificationType = 'success', ...refreshOptions } = {}) {
        if (message) {
            window.AppUi?.notify?.(message, notificationType);
        }

        window.dispatchEvent(new CustomEvent('admin:data-mutated', {
            detail: {
                message: message || '',
                tabName: refreshOptions.tabName || resolveCurrentAdminTab(),
                id: refreshOptions.id ?? null
            }
        }));

        return refreshAdminUi(refreshOptions);
    }

    window.handleResponse = handleResponse;
    window.handleError = handleError;
    window.getValueSafe = getValueSafe;
    window.getHttpStatusMessage = getHttpStatusMessage;
    window.getResponseErrorMessage = getResponseErrorMessage;
    window.resolveCurrentAdminTab = resolveCurrentAdminTab;
    window.resolveAdminRoute = resolveAdminRoute;
    window.navigateAdminUrl = navigateAdminUrl;
    window.syncAdminChromeContextFromDocument = syncAdminChromeContextFromDocument;
    window.refreshAdminUi = refreshAdminUi;
    window.handleAdminMutationSuccess = handleAdminMutationSuccess;
})();
