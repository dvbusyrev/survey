(() => {
if (window.__appNavigationLoaded) {
    return;
}

window.__appNavigationLoaded = true;

const NAV_SUBMENU_SUPPRESS_STORAGE_KEY = 'app-nav-submenu-suppressed';
const MOBILE_NAV_OPEN_CLASS = 'mobile-nav-open';
const COMPACT_NAVIGATION_CLASS = 'compact-nav-mode';
const PREPAINT_COMPACT_NAVIGATION_CLASS = 'app-compact-shell';
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

function hasNavigationHost() {
    return Boolean(document.getElementById('chrome-navigation'));
}

function syncMobileNavigationToggleButtons() {
    const isOpen = isMobileNavigationOpen();
    const isCompact = isMobileNavigationViewport();
    const hasNavigation = hasNavigationHost();
    document.querySelectorAll('.header-menu-toggle').forEach((button) => {
        button.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        button.setAttribute('aria-label', isOpen ? 'Закрыть навигацию' : 'Открыть навигацию');
        button.hidden = !hasNavigation || !isCompact;
    });
}

function setMobileNavigationOpen(nextOpen) {
    const shouldOpen = Boolean(nextOpen) && hasNavigationHost() && isMobileNavigationViewport();
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
        document.documentElement.classList.remove(PREPAINT_COMPACT_NAVIGATION_CLASS);
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
    document.documentElement.classList.remove(PREPAINT_COMPACT_NAVIGATION_CLASS);

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

function renderNavigation(host, { activeTab, userRole }) {
    const isAdmin = userRole === 'admin';
    const isModifiedNavigationEvent = (event) => event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;

    const isSurveySectionActive = isAdmin
        ? ['get_surveys', 'add_survey', 'list_answers_users', 'archived_surveys'].includes(activeTab)
        : ['active', 'archived', 'answers_tab', 'archived_surveys_for_user'].includes(activeTab);
    const isSurveyTemplateSectionActive = isAdmin
        && ['survey_templates', 'add_survey_template', 'planned_survey_templates', 'add_planned_survey_template', 'archived_survey_templates'].includes(activeTab);
    const isOrganizationSectionActive = ['get_organization', 'organization_surveys', 'add_organization', 'archive_list_organizations'].includes(activeTab);
    const isEmailSectionActive = ['email', 'email_new'].includes(activeTab);
    const isSettingsSectionActive = ['email_settings', 'theme_settings', 'survey_auto_creation'].includes(activeTab);

    const navigate = (link) => {
        const href = link?.getAttribute?.('href') || '';
        if (!href || href === '#') {
            return;
        }

        window.AppScrollState?.prepareNavigation({ carry: true });
        window.location.href = href;
    };

    const templateId = isAdmin ? 'nav-template-admin' : 'nav-template-user';
    const template = document.getElementById(templateId);
    if (!host || !template?.content?.firstElementChild) {
        return null;
    }

    evaluateNavigationLayout();
    const expectedRole = isAdmin ? 'admin' : 'user';
    const existingNav = host.querySelector(':scope > .admin-nav');
    const canHydrateExistingNav = existingNav
        && existingNav.dataset.navigationRole === expectedRole
        && existingNav.dataset.navigationMounted !== 'true';
    const nav = canHydrateExistingNav
        ? existingNav
        : template.content.firstElementChild.cloneNode(true);
    if (!canHydrateExistingNav) {
        host.replaceChildren(nav);
    }
    nav.dataset.navigationMounted = 'true';
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
            : navClass === 'survey-templates'
                ? isSurveyTemplateSectionActive
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
            navigate(event.currentTarget);
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
            closeMobileNavIfNeeded();
            navigate(event.currentTarget);
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
