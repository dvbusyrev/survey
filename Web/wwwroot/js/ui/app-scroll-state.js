(() => {
    if (window.AppScrollState) {
        return;
    }

    const POSITIONS_KEY = 'app-scroll-state:positions:v1';
    const CARRY_KEY = 'app-scroll-state:carry:v1';
    const MAX_CARRY_AGE_MS = 30_000;

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

    function getScrollTop() {
        return Math.max(
            window.scrollY || 0,
            document.documentElement?.scrollTop || 0,
            document.body?.scrollTop || 0
        );
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
        const applyScroll = () => window.scrollTo(0, targetTop);

        window.requestAnimationFrame(() => window.requestAnimationFrame(applyScroll));
        window.setTimeout(applyScroll, 80);
    }

    function restorePosition(path = getCurrentPath(), options = {}) {
        const preferCarry = options.preferCarry === true;
        const normalizedPath = normalizePath(path);

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
    window.addEventListener('pagehide', saveCurrentPosition);
    window.addEventListener('beforeunload', saveCurrentPosition);
    window.addEventListener('pageshow', () => restoreCurrentPosition({ preferCarry: true }));

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => restoreCurrentPosition({ preferCarry: true }), { once: true });
    } else {
        restoreCurrentPosition({ preferCarry: true });
    }
})();
