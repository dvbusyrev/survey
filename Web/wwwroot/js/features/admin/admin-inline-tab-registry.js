const listPageDefinitions = Object.freeze({
    get_surveys: { pathname: '/survey' },
    list_answers_users: { pathname: '/survey/answer' },
    archived_surveys: { pathname: '/survey/archive' },
    get_logs: { pathname: '/logs' },
    get_users: { pathname: '/users' },
    get_organization: { pathname: '/organizations' },
    archive_list_organizations: { pathname: '/organizations/archive' },
    archived_users: { pathname: '/users/archive', activeTab: 'archived_users' },
    archive_list_users: { pathname: '/users/archive', activeTab: 'archived_users' }
});

const staticPageDefinitions = Object.freeze({
    open_statistics: { pathname: '/statistics' },
    organization_surveys: { pathname: '/organizations/survey' },
    help: { pathname: '/help' },
    reports: { pathname: '/reports' },
    survey_auto_creation: { pathname: '/settings/survey-creation' },
    theme_settings: { pathname: '/settings/theme' },
    email: { pathname: '/email', activeTab: 'email_new' },
    email_new: { pathname: '/email', activeTab: 'email_new' },
    email_settings: { pathname: '/settings/email' }
});

const entityPageDefinitions = Object.freeze({
    get_survey_signatures: {
        pathname: (id) => `/survey/${id}/signatures`,
        missingIdMessage: 'ID анкеты не указан.'
    }
});

function hasIdentifier(value) {
    return value !== null && value !== undefined && value !== '';
}

export function resolveAdminTabPageRequest(tab, id, buildListRequestUrl) {
    const listDefinition = listPageDefinitions[tab];
    if (listDefinition) {
        return {
            url: buildListRequestUrl(listDefinition.pathname, id),
            activeTab: listDefinition.activeTab || tab
        };
    }

    const staticDefinition = staticPageDefinitions[tab];
    if (staticDefinition) {
        return {
            url: staticDefinition.pathname,
            activeTab: staticDefinition.activeTab || tab
        };
    }

    const entityDefinition = entityPageDefinitions[tab];
    if (!entityDefinition) {
        return null;
    }

    if (!hasIdentifier(id)) {
        throw new Error(entityDefinition.missingIdMessage);
    }

    return {
        url: entityDefinition.pathname(id),
        activeTab: entityDefinition.activeTab || tab
    };
}
