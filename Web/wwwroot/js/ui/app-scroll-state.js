(() => {
    if (window.AppScrollState) {
        return;
    }

    const POSITIONS_KEY = 'app-scroll-state:positions:v1';
    const CARRY_KEY = 'app-scroll-state:carry:v1';
    const MAX_CARRY_AGE_MS = 30_000;
    const SCROLLABLE_OVERFLOW_RE = /(auto|scroll|overlay)/;
    const SCROLL_LOCK_SELECTOR = [
        '.modal.modal--visible',
        '.modal-overlay.active',
        '.notification-overlay.active',
        '.site-confirm-overlay.is-open',
        '.survey-period-filter__popover:not(.is-hidden)',
        '[data-role$="-dropdown-menu"]:not(.is-hidden)',
        '[role="dialog"]:not(.is-hidden)'
    ].join(',');

    function normalizePath(path) {
        if (!path) {
            return '/';
        }

        try {
            const url = new URL(path, window.location.origin);
            path = `${url.pathname}${url.search}`;
        } catch (error) {
            path = String(path);
        }

        return path.length > 1 && path.endsWith('/')
            ? path.slice(0, -1)
            : path;
    }

    function readJson(key, fallback) {
        try {
            const rawValue = window.sessionStorage.getItem(key);
            return rawValue ? JSON.parse(rawValue) : fallback;
        } catch (error) {
            return fallback;
        }
    }

    function writeJson(key, value) {
        try {
            window.sessionStorage.setItem(key, JSON.stringify(value));
        } catch (error) {
            // Ignore storage write failures.
        }
    }

    function removeStorageKey(key) {
        try {
            window.sessionStorage.removeItem(key);
        } catch (error) {
            // Ignore storage remove failures.
        }
    }

    function getCurrentPath() {
        return normalizePath(`${window.location.pathname}${window.location.search}`);
    }

    function getContentScroller() {
        return document.getElementById('content_admin')
            || document.getElementById('content_user')
            || null;
    }

    function getPrimaryScroller() {
        return getContentScroller()
            || document.scrollingElement
            || document.documentElement
            || document.body;
    }

    function isDocumentScroller(scroller) {
        return !scroller
            || scroller === document.documentElement
            || scroller === document.body
            || scroller === document.scrollingElement;
    }

    function getElementFromTarget(target) {
        if (target instanceof Element) {
            return target;
        }

        return target?.parentElement || null;
    }

    function isScrollableElement(element) {
        if (!element || element === document.body || element === document.documentElement) {
            return false;
        }

        const style = window.getComputedStyle(element);
        return SCROLLABLE_OVERFLOW_RE.test(style.overflowY || '')
            && element.scrollHeight > element.clientHeight + 1;
    }

    function findScrollableAncestor(target, stopAt = null) {
        let element = getElementFromTarget(target);

        while (element && element !== document.body && element !== document.documentElement) {
            if (element === stopAt) {
                return element;
            }

            if (isScrollableElement(element)) {
                return element;
            }

            element = element.parentElement;
        }

        return null;
    }

    function isInsideOpenOverlay(target) {
        const element = getElementFromTarget(target);
        return Boolean(element?.closest?.(SCROLL_LOCK_SELECTOR));
    }

    function getScrollTop() {
        const scroller = getPrimaryScroller();
        if (isDocumentScroller(scroller)) {
            return Math.max(
                window.scrollY || 0,
                document.documentElement?.scrollTop || 0,
                document.body?.scrollTop || 0
            );
        }

        return Math.max(0, scroller.scrollTop || 0);
    }

    function persistPosition(path, scrollTop) {
        const normalizedPath = normalizePath(path);
        const positions = readJson(POSITIONS_KEY, {});
        positions[normalizedPath] = Math.max(0, Math.round(scrollTop));
        writeJson(POSITIONS_KEY, positions);
    }

    function getSavedPosition(path = getCurrentPath()) {
        const positions = readJson(POSITIONS_KEY, {});
        const savedPosition = positions[normalizePath(path)];
        return Number.isFinite(savedPosition) ? savedPosition : null;
    }

    function saveCurrentPosition() {
        const scrollTop = getScrollTop();
        persistPosition(getCurrentPath(), scrollTop);
        return scrollTop;
    }

    function rememberCarryPosition() {
        const scrollTop = saveCurrentPosition();
        writeJson(CARRY_KEY, {
            scrollTop,
            createdAt: Date.now()
        });
        return scrollTop;
    }

    function takeCarryPosition() {
        const carryState = readJson(CARRY_KEY, null);
        if (!carryState || !Number.isFinite(carryState.scrollTop)) {
            removeStorageKey(CARRY_KEY);
            return null;
        }

        if (Date.now() - Number(carryState.createdAt || 0) > MAX_CARRY_AGE_MS) {
            removeStorageKey(CARRY_KEY);
            return null;
        }

        removeStorageKey(CARRY_KEY);
        return Math.max(0, Math.round(carryState.scrollTop));
    }

    function scrollToPosition(scrollTop) {
        if (!Number.isFinite(scrollTop)) {
            return;
        }

        const targetTop = Math.max(0, Math.round(scrollTop));
        const applyScroll = () => {
            const scroller = getPrimaryScroller();
            if (isDocumentScroller(scroller)) {
                window.scrollTo(0, targetTop);
                return;
            }

            scroller.scrollTop = targetTop;
        };

        window.requestAnimationFrame(() => window.requestAnimationFrame(applyScroll));
        window.setTimeout(applyScroll, 80);
    }

    function handleDocumentWheel(event) {
        if (event.defaultPrevented || document.body?.classList.contains('modal-open')) {
            return;
        }

        const scroller = getContentScroller();
        if (!scroller || !isScrollableElement(scroller)) {
            return;
        }

        if (isInsideOpenOverlay(event.target)) {
            return;
        }

        const localScroller = findScrollableAncestor(event.target, scroller);
        if (localScroller && localScroller !== scroller) {
            return;
        }

        const maxScrollTop = Math.max(0, scroller.scrollHeight - scroller.clientHeight);
        const deltaY = event.deltaMode === WheelEvent.DOM_DELTA_LINE
            ? event.deltaY * 16
            : (event.deltaMode === WheelEvent.DOM_DELTA_PAGE ? event.deltaY * scroller.clientHeight : event.deltaY);
        const nextScrollTop = Math.min(maxScrollTop, Math.max(0, scroller.scrollTop + deltaY));
        const targetElement = getElementFromTarget(event.target);
        const isInsideContentScroller = scroller.contains(targetElement);

        if (isInsideContentScroller) {
            if (nextScrollTop === scroller.scrollTop && deltaY !== 0) {
                event.preventDefault();
            }
            return;
        }

        if (nextScrollTop !== scroller.scrollTop) {
            scroller.scrollTop = nextScrollTop;
        }
        event.preventDefault();
    }

    function restorePosition(path = getCurrentPath(), options = {}) {
        const preferCarry = options.preferCarry === true;
        const normalizedPath = normalizePath(path);

        if (window.location.hash) {
            takeCarryPosition();
            return null;
        }

        let targetScrollTop = null;
        if (preferCarry) {
            targetScrollTop = takeCarryPosition();
        }

        if (!Number.isFinite(targetScrollTop)) {
            targetScrollTop = getSavedPosition(normalizedPath);
        }

        if (!Number.isFinite(targetScrollTop)) {
            return null;
        }

        persistPosition(normalizedPath, targetScrollTop);
        scrollToPosition(targetScrollTop);
        return targetScrollTop;
    }

    function prepareNavigation(options = {}) {
        if (options.carry === true) {
            return rememberCarryPosition();
        }

        return saveCurrentPosition();
    }

    function isModifiedClick(event) {
        return event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
    }

    function handleDocumentClick(event) {
        if (event.defaultPrevented || isModifiedClick(event)) {
            return;
        }

        const link = event.target.closest('a[href]');
        if (!link) {
            return;
        }

        if (link.target && link.target !== '_self') {
            return;
        }

        if (link.hasAttribute('download')) {
            return;
        }

        const href = link.getAttribute('href') || '';
        if (!href || href.startsWith('#') || href.startsWith('javascript:') || href.startsWith('mailto:') || href.startsWith('tel:')) {
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

        const targetPath = normalizePath(`${targetUrl.pathname}${targetUrl.search}`);
        if (targetPath === getCurrentPath() && !targetUrl.hash) {
            return;
        }

        if (link.dataset.scrollAnchor === 'true') {
            saveCurrentPosition();
            return;
        }

        prepareNavigation({ carry: true });
    }

    function handleFormSubmit(event) {
        if (event.defaultPrevented) {
            return;
        }

        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const method = (form.getAttribute('method') || 'get').toLowerCase();
        if (method !== 'get') {
            return;
        }

        let actionUrl;
        try {
            actionUrl = new URL(form.getAttribute('action') || window.location.href, window.location.href);
        } catch (error) {
            return;
        }

        if (actionUrl.origin !== window.location.origin) {
            return;
        }

        prepareNavigation({ carry: true });
    }

    function restoreCurrentPosition(options = {}) {
        return restorePosition(getCurrentPath(), options);
    }

    window.AppScrollState = {
        getCurrentPath,
        getSavedPosition,
        prepareNavigation,
        restoreCurrentPosition,
        restorePosition,
        saveCurrentPosition
    };

    document.addEventListener('click', handleDocumentClick);
    document.addEventListener('submit', handleFormSubmit);
    document.addEventListener('wheel', handleDocumentWheel, { passive: false });
    window.addEventListener('pagehide', saveCurrentPosition);
    window.addEventListener('beforeunload', saveCurrentPosition);
    window.addEventListener('pageshow', () => restoreCurrentPosition({ preferCarry: true }));

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => restoreCurrentPosition({ preferCarry: true }), { once: true });
    } else {
        restoreCurrentPosition({ preferCarry: true });
    }
})();
