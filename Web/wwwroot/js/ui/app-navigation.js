(() => {
if (window.__appNavigationLoaded) {
    return;
}

window.__appNavigationLoaded = true;

const NAV_SUBMENU_SUPPRESS_STORAGE_KEY = 'app-nav-submenu-suppressed';

function getNavigationSuppressedTab() {
    try {
        return window.sessionStorage.getItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY) || '';
    } catch (error) {
        return '';
    }
}

function isNavigationSubmenuSuppressed(tab) {
    const suppressedTab = getNavigationSuppressedTab();
    if (!suppressedTab) {
        return false;
    }

    if (!tab) {
        return true;
    }

    return suppressedTab === tab;
}

function setNavigationSubmenuSuppressed(tab) {
    try {
        if (tab) {
            window.sessionStorage.setItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY, String(tab));
            return;
        }

        window.sessionStorage.removeItem(NAV_SUBMENU_SUPPRESS_STORAGE_KEY);
    } catch (error) {
        // Ignore storage access issues and fall back to in-memory behavior.
    }
}

function closeNavigationSubmenus(root) {
    const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
    scope.querySelectorAll('.nav-item.has-submenu.submenu-open').forEach((item) => {
        item.classList.remove('submenu-open');
    });
}

function suppressNavigationSubmenus(root, tab) {
    setNavigationSubmenuSuppressed(tab || '');
    closeNavigationSubmenus(root);
}

function releaseNavigationSubmenuSuppression() {
    setNavigationSubmenuSuppressed('');
}

window.isNavigationSubmenuSuppressed = isNavigationSubmenuSuppressed;
window.suppressNavigationSubmenus = suppressNavigationSubmenus;
window.releaseNavigationSubmenuSuppression = releaseNavigationSubmenuSuppression;

function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
    const isAdmin = userRole === 'admin';
    const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;

    const isSurveySectionActive = isAdmin
        ? ['get_surveys', 'add_survey', 'list_answers_users', 'archived_surveys'].includes(activeTab)
        : ['active', 'archived', 'answers_tab', 'archived_surveys_for_user'].includes(activeTab);
    const isOrganizationSectionActive = ['get_organization', 'organization_surveys', 'add_organization', 'archive_list_organizations'].includes(activeTab);
    const isEmailSectionActive = ['email', 'email_new', 'email_settings'].includes(activeTab);

    const navigate = (tab) => {
        if (tab === 'add_user') {
            const tryOpenAddUserModal = () => {
                if (typeof window.openAddUserModal === 'function' && document.getElementById('addUserModal')) {
                    window.openAddUserModal();
                    return true;
                }
                return false;
            };

            if (tryOpenAddUserModal()) {
                return;
            }

            if (typeof openTab === 'function') {
                openTab('get_users', null, { scrollMode: 'carry' });

                let attempts = 0;
                const timer = window.setInterval(() => {
                    attempts += 1;
                    if (tryOpenAddUserModal() || attempts >= 30) {
                        window.clearInterval(timer);
                    }
                }, 200);
                return;
            }

            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/users';
            return;
        }

        if (tab === 'add_organization') {
            const tryOpenAddOrganizationModal = () => {
                if (typeof window.openAddOrganizationModal === 'function' && document.getElementById('addOrganizationModal')) {
                    window.openAddOrganizationModal();
                    return true;
                }
                return false;
            };

            if (tryOpenAddOrganizationModal()) {
                return;
            }

            if (typeof openTab === 'function') {
                openTab('get_organization', null, { scrollMode: 'carry' });

                let attempts = 0;
                const timer = window.setInterval(() => {
                    attempts += 1;
                    if (tryOpenAddOrganizationModal() || attempts >= 30) {
                        window.clearInterval(timer);
                    }
                }, 200);
                return;
            }

            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/organizations';
            return;
        }

        if (typeof openTab === 'function') {
            openTab(tab, null, { scrollMode: 'carry' });
            return;
        }

        if (tab === 'help') {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/help';
            return;
        }

        if (tab === 'download_logs') {
            window.location.href = '/logs/export';
            return;
        }

        if ((tab === 'active' || tab === 'answers_tab') && userId) {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/my-surveys';
            return;
        }

        if ((tab === 'archived' || tab === 'archived_surveys_for_user') && userId) {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/my-surveys/archive';
            return;
        }

        const routes = {
            get_surveys: '/surveys',
            add_survey: '/surveys/create',
            list_answers_users: '/surveys/answers',
            archived_surveys: '/surveys/archive',
            open_statistics: '/statistics',
            get_users: '/users',
            archived_users: '/users/archive',
            get_organization: '/organizations',
            organization_surveys: '/organizations/surveys',
            archive_list_organizations: '/organizations/archive',
            reports: '/reports',
            email: '/mail',
            email_new: '/mail',
            email_settings: '/mail/configuration',
            get_logs: '/logs'
        };

        if (routes[tab]) {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = routes[tab];
            return;
        }

        if (tab === 'monthly_summary_report') {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/reports';
            return;
        }

        if (tab.startsWith('quarterly_report_q')) {
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/reports';
        }
    };

    const templateId = isAdmin ? 'nav-template-admin' : 'nav-template-user';
    const template = document.getElementById(templateId);
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    host.innerHTML = '';
    const nav = template.content.firstElementChild.cloneNode(true);
    host.appendChild(nav);

    const closeSubmenus = () => closeNavigationSubmenus(nav);

    nav.querySelectorAll('.nav-item').forEach((item) => {
        const tab = item.dataset.tab || '';
        const navClass = item.dataset.navClass || '';
        const isActive = navClass === 'surveys'
            ? isSurveySectionActive
            : navClass === 'organizations'
                ? isOrganizationSectionActive
                : navClass === 'email'
                    ? isEmailSectionActive
                : tab === activeTab;
        item.classList.toggle('active', isActive);
    });

    nav.querySelectorAll('.submenu-item').forEach((subItem) => {
        subItem.classList.toggle('active', (subItem.dataset.tab || '') === activeTab);
    });

    nav.querySelectorAll('.nav-item.has-submenu').forEach((item) => {
        const itemTab = item.dataset.tab || '';
        const onEnter = () => {
            if (isNavigationSubmenuSuppressed(itemTab)) {
                releaseNavigationSubmenuSuppression();
                item.classList.remove('submenu-open');
                return;
            }

            if (isNavigationSubmenuSuppressed()) {
                releaseNavigationSubmenuSuppression();
            }

            item.classList.add('submenu-open');
        };
        const onLeave = () => {
            item.classList.remove('submenu-open');
        };
        item.addEventListener('mouseenter', onEnter);
        item.addEventListener('mouseleave', onLeave);
    });

    const navLeaveHandler = () => {
        closeSubmenus();
        releaseNavigationSubmenuSuppression();
    };
    nav.addEventListener('mouseleave', navLeaveHandler);

    nav.querySelectorAll('.nav-link').forEach((link) => {
        link.addEventListener('click', (event) => {
            if (isModifiedNavigationEvent(event)) {
                closeSubmenus();
                return;
            }

            event.preventDefault();
            const item = event.currentTarget.closest('.nav-item');
            if (!item) {
                return;
            }

            if (item.classList.contains('has-submenu') && item.dataset.disableDirectNav === 'true') {
                releaseNavigationSubmenuSuppression();
                const shouldOpen = !item.classList.contains('submenu-open');
                closeSubmenus();
                if (shouldOpen) {
                    item.classList.add('submenu-open');
                }
                return;
            }

            suppressNavigationSubmenus(nav, item.classList.contains('has-submenu') ? item.dataset.tab || '' : '');
            navigate(item.dataset.tab || '');
        });
    });

    nav.querySelectorAll('.submenu-link').forEach((link) => {
        link.addEventListener('click', (event) => {
            if (isModifiedNavigationEvent(event)) {
                closeSubmenus();
                return;
            }

            event.preventDefault();
            const ownerItem = event.currentTarget.closest('.nav-item.has-submenu');
            suppressNavigationSubmenus(nav, ownerItem?.dataset?.tab || '');
            const item = event.currentTarget.closest('.submenu-item');
            navigate(item?.dataset?.tab || '');
        });
    });

    const onPointerDown = (event) => {
        if (!event.target.closest('.admin-nav')) {
            closeSubmenus();
            releaseNavigationSubmenuSuppression();
        }
    };
    document.addEventListener('pointerdown', onPointerDown);

    return () => {
        document.removeEventListener('pointerdown', onPointerDown);
        nav.removeEventListener('mouseleave', navLeaveHandler);
        host.innerHTML = '';
    };
}

window.mountNavigation = function mountNavigation(host, props) {
    return renderNavigation(host, props || {});
};
})();
