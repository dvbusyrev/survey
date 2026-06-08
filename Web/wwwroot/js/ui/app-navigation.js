(() => {
if (window.__appNavigationLoaded) {
    return;
}

window.__appNavigationLoaded = true;

const NAV_SUBMENU_SUPPRESS_STORAGE_KEY = 'app-nav-submenu-suppressed';
const MOBILE_NAV_OPEN_CLASS = 'mobile-nav-open';
const COMPACT_NAVIGATION_CLASS = 'compact-nav-mode';
const NAVIGATION_LAYOUT_SYNC_CLASS = 'nav-layout-sync';
const MOBILE_NAV_MEDIA_QUERY = '(max-width: 900px)';
const COMPACT_NAVIGATION_BREAKPOINT_PX = 1220;
let navigationLayoutFrameId = 0;
let navigationLayoutSyncFrameId = 0;
let visualViewportResizeHandler = null;

function isMobileNavigationViewport() {
    return typeof window.matchMedia === 'function'
        ? window.matchMedia(MOBILE_NAV_MEDIA_QUERY).matches || document.body.classList.contains(COMPACT_NAVIGATION_CLASS)
        : window.innerWidth <= 900;
}

function isMobileNavigationOpen() {
    return document.body.classList.contains(MOBILE_NAV_OPEN_CLASS);
}

function syncMobileNavigationToggleButtons() {
    const isOpen = isMobileNavigationOpen();
    const isCompact = isMobileNavigationViewport();
    document.querySelectorAll('.header-menu-toggle').forEach((button) => {
        button.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        button.setAttribute('aria-label', isOpen ? 'Закрыть навигацию' : 'Открыть навигацию');
        button.hidden = !isCompact;
    });
}

function setMobileNavigationOpen(nextOpen) {
    const shouldOpen = Boolean(nextOpen) && isMobileNavigationViewport();
    document.body.classList.toggle(MOBILE_NAV_OPEN_CLASS, shouldOpen);
    syncMobileNavigationToggleButtons();
}

function closeMobileNavigation() {
    setMobileNavigationOpen(false);
}

function toggleMobileNavigation() {
    setMobileNavigationOpen(!isMobileNavigationOpen());
}

function getViewportWidth() {
    if (window.visualViewport?.width) {
        return window.visualViewport.width;
    }

    return window.innerWidth || document.documentElement.clientWidth || 0;
}

function measureCompactNavigationNeed() {
    return getViewportWidth() <= COMPACT_NAVIGATION_BREAKPOINT_PX;
}

function syncNavigationLayoutWithoutAnimation() {
    if (!document.body) {
        return;
    }

    document.body.classList.add(NAVIGATION_LAYOUT_SYNC_CLASS);
    if (navigationLayoutSyncFrameId) {
        window.cancelAnimationFrame(navigationLayoutSyncFrameId);
    }

    navigationLayoutSyncFrameId = window.requestAnimationFrame(() => {
        navigationLayoutSyncFrameId = window.requestAnimationFrame(() => {
            navigationLayoutSyncFrameId = 0;
            document.body?.classList.remove(NAVIGATION_LAYOUT_SYNC_CLASS);
        });
    });
}

function evaluateNavigationLayout() {
    if (!document.body) {
        return;
    }

    const wasCompact = document.body.classList.contains(COMPACT_NAVIGATION_CLASS);
    const isNarrowViewport = typeof window.matchMedia === 'function'
        ? window.matchMedia(MOBILE_NAV_MEDIA_QUERY).matches
        : window.innerWidth <= 900;

    if (isNarrowViewport) {
        if (wasCompact) {
            syncNavigationLayoutWithoutAnimation();
        }
        document.body.classList.remove(COMPACT_NAVIGATION_CLASS);
        syncMobileNavigationToggleButtons();
        return;
    }

    if (wasCompact) {
        document.body.classList.remove(COMPACT_NAVIGATION_CLASS);
    }

    const shouldCompact = measureCompactNavigationNeed();
    if (shouldCompact !== wasCompact) {
        syncNavigationLayoutWithoutAnimation();
    }

    document.body.classList.toggle(COMPACT_NAVIGATION_CLASS, shouldCompact);

    if (!shouldCompact && isMobileNavigationOpen()) {
        closeMobileNavigation();
    }

    syncMobileNavigationToggleButtons();
}

function queueNavigationLayoutEvaluation() {
    if (navigationLayoutFrameId) {
        window.cancelAnimationFrame(navigationLayoutFrameId);
    }

    navigationLayoutFrameId = window.requestAnimationFrame(() => {
        navigationLayoutFrameId = 0;
        evaluateNavigationLayout();
    });
}

function attachViewportObservers(onResize) {
    if (!window.visualViewport) {
        return;
    }

    visualViewportResizeHandler = () => {
        onResize();
    };

    window.visualViewport.addEventListener('resize', visualViewportResizeHandler);
    window.visualViewport.addEventListener('scroll', visualViewportResizeHandler);
}

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
window.closeMobileNavigation = closeMobileNavigation;
window.toggleMobileNavigation = toggleMobileNavigation;
window.queueNavigationLayoutEvaluation = queueNavigationLayoutEvaluation;
window.isAppMobileNavigationViewport = isMobileNavigationViewport;

function renderNavigation(host, { openTab, activeTab, userRole, userId }) {
    const isAdmin = userRole === 'admin';
    const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;

    const isSurveySectionActive = isAdmin
        ? ['get_surveys', 'add_survey', 'list_answers_users', 'archived_surveys'].includes(activeTab)
        : ['active', 'archived', 'answers_tab', 'archived_surveys_for_user'].includes(activeTab);
    const isOrganizationSectionActive = ['get_organization', 'organization_surveys', 'add_organization', 'archive_list_organizations'].includes(activeTab);
    const isEmailSectionActive = ['email', 'email_new'].includes(activeTab);
    const isSettingsSectionActive = ['email_settings', 'theme_settings', 'survey_auto_creation'].includes(activeTab);

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
            window.open('/help/download', '_blank');
            window.AppScrollState?.prepareNavigation({ carry: true });
            window.location.href = '/help';
            return;
        }

        if (tab === 'download_logs') {
            window.location.href = '/event-log/export';
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
            survey_auto_creation: '/survey-auto-creation',
            theme_settings: '/theme/configuration',
            email: '/mail',
            email_new: '/mail',
            email_settings: '/mail/configuration',
            get_logs: '/event-log'
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

    evaluateNavigationLayout();
    host.innerHTML = '';
    const nav = template.content.firstElementChild.cloneNode(true);
    host.appendChild(nav);
    syncMobileNavigationToggleButtons();

    const closeSubmenus = () => closeNavigationSubmenus(nav);
    const closeMobileNavIfNeeded = () => {
        if (isMobileNavigationViewport()) {
            closeMobileNavigation();
        }
    };

    nav.querySelectorAll('.nav-item').forEach((item) => {
        const tab = item.dataset.tab || '';
        const navClass = item.dataset.navClass || '';
        const isActive = navClass === 'surveys'
            ? isSurveySectionActive
            : navClass === 'organizations'
                ? isOrganizationSectionActive
                : navClass === 'email'
                    ? isEmailSectionActive
                    : navClass === 'settings'
                        ? isSettingsSectionActive
                : tab === activeTab;
        item.classList.toggle('active', isActive);
    });

    nav.querySelectorAll('.submenu-item').forEach((subItem) => {
        subItem.classList.toggle('active', (subItem.dataset.tab || '') === activeTab);
    });

    nav.querySelectorAll('.nav-item.has-submenu').forEach((item) => {
        const itemTab = item.dataset.tab || '';
        const onEnter = () => {
            if (isMobileNavigationViewport()) {
                return;
            }

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
            if (isMobileNavigationViewport()) {
                return;
            }

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

            if (isMobileNavigationViewport() && item.classList.contains('has-submenu')) {
                const shouldOpen = !item.classList.contains('submenu-open');
                closeSubmenus();
                if (shouldOpen) {
                    item.classList.add('submenu-open');
                }
                return;
            }

            suppressNavigationSubmenus(nav, item.classList.contains('has-submenu') ? item.dataset.tab || '' : '');
            closeMobileNavIfNeeded();
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
            closeMobileNavIfNeeded();
            navigate(item?.dataset?.tab || '');
        });
    });

    const menuToggleButton = document.querySelector('.header-menu-toggle');
    const menuToggleHandler = (event) => {
        if (!isMobileNavigationViewport()) {
            return;
        }

        event.preventDefault();
        toggleMobileNavigation();
    };

    if (menuToggleButton) {
        menuToggleButton.addEventListener('click', menuToggleHandler);
    }

    const navOverlayClickHandler = (event) => {
        if (!isMobileNavigationViewport() || event.target !== host) {
            return;
        }

        closeMobileNavigation();
    };
    host.addEventListener('click', navOverlayClickHandler);

    const onEscape = (event) => {
        if (event.key === 'Escape') {
            closeMobileNavigation();
        }
    };
    document.addEventListener('keydown', onEscape);

    const onPointerDown = (event) => {
        if (isMobileNavigationViewport()) {
            return;
        }

        if (!event.target.closest('.admin-nav')) {
            closeSubmenus();
            releaseNavigationSubmenuSuppression();
        }
    };
    document.addEventListener('pointerdown', onPointerDown);

    const onResize = () => {
        if (!isMobileNavigationViewport()) {
            closeMobileNavigation();
            closeSubmenus();
        }
        syncMobileNavigationToggleButtons();
        queueNavigationLayoutEvaluation();
    };
    window.addEventListener('resize', onResize);
    attachViewportObservers(onResize);

    return () => {
        if (menuToggleButton) {
            menuToggleButton.removeEventListener('click', menuToggleHandler);
        }
        host.removeEventListener('click', navOverlayClickHandler);
        document.removeEventListener('keydown', onEscape);
        document.removeEventListener('pointerdown', onPointerDown);
        window.removeEventListener('resize', onResize);
        if (visualViewportResizeHandler && window.visualViewport) {
            window.visualViewport.removeEventListener('resize', visualViewportResizeHandler);
            window.visualViewport.removeEventListener('scroll', visualViewportResizeHandler);
            visualViewportResizeHandler = null;
        }
        nav.removeEventListener('mouseleave', navLeaveHandler);
        closeMobileNavigation();
        host.innerHTML = '';
    };
}

window.mountNavigation = function mountNavigation(host, props) {
    return renderNavigation(host, props || {});
};

window.addEventListener('load', () => {
    queueNavigationLayoutEvaluation();
});
})();
