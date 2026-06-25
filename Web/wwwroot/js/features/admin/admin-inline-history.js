    function normalizePathname(pathname) {
        if (!pathname) {
            return '/';
        }

        return pathname.length > 1 && pathname.endsWith('/')
            ? pathname.slice(0, -1)
            : pathname;
    }

    function normalizeLocationUrl(pathname, search = '') {
        const normalizedPath = normalizePathname(pathname);
        return `${normalizedPath}${search || ''}`;
    }

    function normalizeLogsHistoryId(value) {
        const rawValue = String(value || '').trim();
        if (!rawValue) {
            return null;
        }

        const normalizedValue = rawValue.startsWith('?')
            ? rawValue.slice(1)
            : rawValue;

        return normalizedValue.length > 0
            ? normalizedValue
            : null;
    }

    function resolveQueryHistoryId(pathname, value, preserveCurrentWhenMissing = false) {
        if (value === undefined) {
            return preserveCurrentWhenMissing
                && normalizePathname(window.location.pathname) === normalizePathname(pathname)
                ? normalizeLogsHistoryId(window.location.search)
                : null;
        }

        return normalizeLogsHistoryId(value);
    }

    function buildQueryHistoryEntry(tab, pathname, value, options = {}) {
        const query = resolveQueryHistoryId(
            pathname,
            value,
            options.preserveCurrentWhenMissing === true
        );
        return {
            tab,
            id: query,
            url: query ? `${pathname}?${query}` : pathname
        };
    }

    function buildAdminHistoryEntry(tab, id = undefined, modalData = null) {
        const surveyId = id ?? modalData?.id_survey ?? null;
        const userId = id ?? modalData?.id_user ?? null;
        const organizationId = id ?? modalData?.id_organization ?? modalData?.organizationId ?? null;

        switch (tab) {
            case 'get_surveys':
                return buildQueryHistoryEntry(tab, '/survey', id, { preserveCurrentWhenMissing: id === undefined });
            case 'list_answers_users':
                return buildQueryHistoryEntry(tab, '/survey/answer', id, { preserveCurrentWhenMissing: id === undefined });
            case 'archived_surveys':
                return buildQueryHistoryEntry(tab, '/survey/archive', id, { preserveCurrentWhenMissing: id === undefined });
            case 'get_survey_signatures':
                return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/signatures` } : null;
            case 'add_survey':
                return { tab, id: null, url: '/survey/create' };
            case 'copy_survey':
                return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/copy` } : null;
            case 'update_survey':
                return surveyId ? { tab, id: surveyId, url: `/survey/${surveyId}/edit` } : null;
            case 'update_archived_survey':
                return surveyId ? { tab, id: surveyId, url: `/survey/archive/${surveyId}/edit` } : null;
            case 'open_statistics':
                return { tab, id: null, url: '/statistics' };
            case 'get_users':
                return buildQueryHistoryEntry(tab, '/users', id, { preserveCurrentWhenMissing: id === undefined });
            case 'add_user':
                return { tab, id: null, url: '/users/create' };
            case 'update_user':
                return userId ? { tab, id: userId, url: `/users/${userId}/edit` } : null;
            case 'archived_users':
            case 'archive_list_users':
                return buildQueryHistoryEntry('archived_users', '/users/archive', id, { preserveCurrentWhenMissing: id === undefined });
            case 'get_organization':
                return buildQueryHistoryEntry(tab, '/organizations', id, { preserveCurrentWhenMissing: id === undefined });
            case 'organization_surveys':
                return { tab, id: null, url: '/organizations/survey' };
            case 'add_organization':
                return { tab, id: null, url: '/organizations/create' };
            case 'update_organization':
                return organizationId ? { tab, id: organizationId, url: `/organizations/${organizationId}/edit` } : null;
            case 'archive_list_organizations':
                return buildQueryHistoryEntry(tab, '/organizations/archive', id, { preserveCurrentWhenMissing: id === undefined });
            case 'reports':
                return { tab, id: null, url: '/reports' };
            case 'survey_auto_creation':
                return { tab, id: null, url: '/settings/survey-creation' };
            case 'theme_settings':
                return { tab, id: null, url: '/settings/theme' };
            case 'get_logs':
                return buildQueryHistoryEntry(tab, '/logs', id, { preserveCurrentWhenMissing: id === undefined });
            case 'email':
            case 'email_new':
                return { tab: tab === 'email' ? 'email_new' : tab, id: null, url: '/email' };
            case 'email_settings':
                return { tab, id: null, url: '/settings/email' };
            case 'help':
                return { tab, id: null, url: '/help' };
            default:
                return null;
        }
    }

    function getAdminHistoryEntryFromLocation(pathname, search = '') {
        const normalizedPath = normalizePathname(pathname);

        if (normalizedPath === '/survey' || normalizedPath === '/surveys') {
            return buildAdminHistoryEntry('get_surveys', search || '');
        }

        if (normalizedPath === '/survey/answer' || normalizedPath === '/surveys/answers') {
            return buildAdminHistoryEntry('list_answers_users', search || '');
        }

        if (normalizedPath === '/survey/archive' || normalizedPath === '/surveys/archive') {
            return buildAdminHistoryEntry('archived_surveys', search || '');
        }

        if (normalizedPath === '/survey/create' || normalizedPath === '/surveys/create') {
            return buildAdminHistoryEntry('add_survey');
        }

        if (normalizedPath === '/statistics') {
            return buildAdminHistoryEntry('open_statistics');
        }

        if (normalizedPath === '/users') {
            return buildAdminHistoryEntry('get_users', search || '');
        }

        if (normalizedPath === '/users/create') {
            return buildAdminHistoryEntry('add_user');
        }

        if (normalizedPath === '/users/archive') {
            return buildAdminHistoryEntry('archived_users', search || '');
        }

        if (normalizedPath === '/organizations') {
            return buildAdminHistoryEntry('get_organization', search || '');
        }

        if (normalizedPath === '/organizations/survey' || normalizedPath === '/organizations/surveys') {
            return buildAdminHistoryEntry('organization_surveys');
        }

        if (normalizedPath === '/organizations/create') {
            return buildAdminHistoryEntry('add_organization');
        }

        if (normalizedPath === '/organizations/archive') {
            return buildAdminHistoryEntry('archive_list_organizations', search || '');
        }

        if (normalizedPath === '/reports') {
            return buildAdminHistoryEntry('reports');
        }

        if (normalizedPath === '/settings/survey-creation' || normalizedPath === '/survey-auto-creation') {
            return buildAdminHistoryEntry('survey_auto_creation');
        }

        if (normalizedPath === '/settings/theme'
            || normalizedPath === '/theme/configuration'
            || normalizedPath === '/theme-settings') {
            return buildAdminHistoryEntry('theme_settings');
        }

        if (normalizedPath === '/logs' || normalizedPath === '/event-log') {
            return buildAdminHistoryEntry('get_logs', search || '');
        }

        if (normalizedPath === '/email'
            || normalizedPath === '/mail'
            || normalizedPath === '/mail/new') {
            return buildAdminHistoryEntry('email_new');
        }

        if (normalizedPath === '/settings/email'
            || normalizedPath === '/mail/configuration'
            || normalizedPath === '/mail-settings') {
            return buildAdminHistoryEntry('email_settings');
        }

        if (normalizedPath === '/help') {
            return buildAdminHistoryEntry('help');
        }

        let match = normalizedPath.match(/^\/survey\/(\d+)\/signatures$/)
            || normalizedPath.match(/^\/surveys\/(\d+)\/signatures$/);
        if (match) {
            return buildAdminHistoryEntry('get_survey_signatures', Number(match[1]));
        }

        match = normalizedPath.match(/^\/survey\/archive\/(\d+)\/edit$/)
            || normalizedPath.match(/^\/surveys\/archive\/(\d+)\/edit$/);
        if (match) {
            return buildAdminHistoryEntry('update_archived_survey', Number(match[1]));
        }

        match = normalizedPath.match(/^\/survey\/(\d+)\/edit$/)
            || normalizedPath.match(/^\/surveys\/(\d+)\/edit$/);
        if (match) {
            return buildAdminHistoryEntry('update_survey', Number(match[1]));
        }

        match = normalizedPath.match(/^\/survey\/(\d+)\/copy$/)
            || normalizedPath.match(/^\/surveys\/(\d+)\/copy$/);
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

export {
    normalizePathname,
    normalizeLocationUrl,
    normalizeLogsHistoryId,
    buildAdminHistoryEntry,
    getAdminHistoryEntryFromLocation
};
