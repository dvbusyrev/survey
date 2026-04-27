(function () {
    function normalizePathname(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    function resolveCurrentAdminTab(pathname = window.location.pathname) {
        const normalizedPath = normalizePathname(pathname).toLowerCase();

        if (normalizedPath === '/statistics') {
            return 'open_statistics';
        }

        if (normalizedPath === '/surveys/answers') {
            return 'list_answers_users';
        }

        if (normalizedPath === '/surveys/archive'
            || /^\/surveys\/archive\/\d+\/edit$/.test(normalizedPath)) {
            return 'archived_surveys';
        }

        if (normalizedPath === '/surveys'
            || normalizedPath === '/surveys/create'
            || /^\/surveys\/\d+\/edit$/.test(normalizedPath)
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

        if (normalizedPath === '/organizations'
            || normalizedPath === '/organizations/create'
            || /^\/organizations\/\d+\/edit$/.test(normalizedPath)) {
            return 'get_organization';
        }

        if (normalizedPath === '/reports') {
            return 'reports';
        }

        if (normalizedPath === '/event-log') {
            return 'get_logs';
        }

        if (normalizedPath === '/mail'
            || normalizedPath === '/mail/new') {
            return 'email_new';
        }

        if (normalizedPath === '/mail/configuration'
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
        window.siteNotify(
            'Произошла ошибка: ' + (error.message || 'Попробуйте снова или обратитесь в поддержку'),
            'error'
        );
    }

    function getValueSafe(elementId) {
        const element = document.getElementById(elementId);
        return element ? element.value : '';
    }

    function showNotification(message, isSuccess) {
        window.siteNotify?.(message, isSuccess ? 'success' : 'error');
    }

    function refreshAdminUi({ tabName, id = null, fallbackUrl, options } = {}) {
        const resolvedTabName = tabName || resolveCurrentAdminTab();
        const resolvedOptions = options && typeof options === 'object'
            ? options
            : {};

        if (resolvedTabName && typeof window.refreshAdminTab === 'function') {
            return window.refreshAdminTab(resolvedTabName, id, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry',
                ...resolvedOptions
            });
        }

        if (resolvedTabName && typeof window.handleTabClick === 'function') {
            return window.handleTabClick(resolvedTabName, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'carry',
                ...resolvedOptions
            });
        }

        const resolvedUrl = fallbackUrl || window.location.pathname;
        if (resolvedUrl) {
            window.AppScrollState?.saveCurrentPosition?.();
            window.location.assign(resolvedUrl);
        }

        return null;
    }

    function handleAdminMutationSuccess({ message, notificationType = 'success', ...refreshOptions } = {}) {
        if (message) {
            window.siteNotify?.(message, notificationType);
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
    window.showNotification = showNotification;
    window.getHttpStatusMessage = getHttpStatusMessage;
    window.getResponseErrorMessage = getResponseErrorMessage;
    window.resolveCurrentAdminTab = resolveCurrentAdminTab;
    window.syncAdminChromeContextFromDocument = syncAdminChromeContextFromDocument;
    window.refreshAdminUi = refreshAdminUi;
    window.handleAdminMutationSuccess = handleAdminMutationSuccess;
})();
