(() => {
    if (window.AppNavigationRouter) {
        return;
    }

    const PAGE_STYLE_START = 'app-page-styles-start';
    const PAGE_STYLE_END = 'app-page-styles-end';
    const PAGE_SCRIPT_HOST = 'app-page-scripts';
    const NAVIGATION_STATE_KEY = 'appNavigation';
    const PREFETCH_TTL_MS = 10_000;
    const loadedScriptUrls = new Set();
    const prefetchedDocuments = new Map();
    const prefetchTimers = new WeakMap();
    let navigationSequence = 0;
    let activeController = null;

    function toUrl(value) {
        try {
            return new URL(value, window.location.href);
        } catch (error) {
            return null;
        }
    }

    function getDocumentPath(url) {
        return `${url.pathname}${url.search}${url.hash}`;
    }

    function isExecutableScript(script) {
        const type = (script.getAttribute('type') || '').trim().toLowerCase();
        return !type
            || type === 'text/javascript'
            || type === 'application/javascript'
            || type === 'module';
    }

    function getAbsoluteAssetUrl(value, baseUrl) {
        try {
            return new URL(value, baseUrl).href;
        } catch (error) {
            return '';
        }
    }

    function rememberLoadedScripts(root = document) {
        root.querySelectorAll?.('script[src]').forEach((script) => {
            const scriptUrl = getAbsoluteAssetUrl(script.getAttribute('src'), document.baseURI);
            if (scriptUrl) {
                loadedScriptUrls.add(scriptUrl);
            }
        });
    }

    function getNodesBetween(documentNode, startId, endId) {
        const start = documentNode.getElementById(startId);
        const end = documentNode.getElementById(endId);
        if (!start || !end) {
            return [];
        }

        const nodes = [];
        let current = start.nextElementSibling;
        while (current && current !== end) {
            nodes.push(current);
            current = current.nextElementSibling;
        }
        return nodes;
    }

    function markInitialPageStyles() {
        getNodesBetween(document, PAGE_STYLE_START, PAGE_STYLE_END)
            .filter((node) => node.matches?.('link[rel="stylesheet"]'))
            .forEach((link) => {
                link.dataset.appPageStyle = 'true';
            });
    }

    async function installPageStyles(nextDocument, baseUrl) {
        const requestedLinks = getNodesBetween(nextDocument, PAGE_STYLE_START, PAGE_STYLE_END)
            .filter((node) => node.matches?.('link[rel="stylesheet"]'));
        const requestedUrls = new Set(requestedLinks
            .map((link) => getAbsoluteAssetUrl(link.getAttribute('href'), baseUrl))
            .filter(Boolean));
        const currentLinks = Array.from(document.head.querySelectorAll('link[data-app-page-style="true"]'));
        const currentByUrl = new Map(currentLinks.map((link) => [link.href, link]));
        const loadPromises = [];

        requestedLinks.forEach((sourceLink) => {
            const absoluteUrl = getAbsoluteAssetUrl(sourceLink.getAttribute('href'), baseUrl);
            if (!absoluteUrl || currentByUrl.has(absoluteUrl)) {
                return;
            }

            const link = document.createElement('link');
            Array.from(sourceLink.attributes).forEach((attribute) => {
                if (attribute.name !== 'href') {
                    link.setAttribute(attribute.name, attribute.value);
                }
            });
            link.href = absoluteUrl;
            link.dataset.appPageStyle = 'true';
            loadPromises.push(new Promise((resolve, reject) => {
                link.addEventListener('load', resolve, { once: true });
                link.addEventListener('error', () => reject(new Error(`Не удалось загрузить стили ${absoluteUrl}`)), { once: true });
            }));
            document.head.appendChild(link);
        });

        await Promise.all(loadPromises);
        currentLinks.forEach((link) => {
            if (!requestedUrls.has(link.href)) {
                link.remove();
            }
        });
    }

    async function fetchPageDocument(url, signal) {
        const response = await fetch(url.href, {
            method: 'GET',
            credentials: 'same-origin',
            cache: 'no-store',
            headers: {
                Accept: 'text/html',
                'X-Requested-With': 'AppNavigation'
            },
            signal
        });

        if (!response.ok) {
            throw new Error(`Не удалось открыть страницу (${response.status}).`);
        }

        const contentType = (response.headers.get('content-type') || '').toLowerCase();
        if (!contentType.includes('text/html')) {
            const error = new Error('Ответ не является HTML-страницей.');
            error.requiresFullNavigation = true;
            throw error;
        }

        const responseUrl = toUrl(response.url) || url;
        const html = await response.text();
        const nextDocument = new DOMParser().parseFromString(html, 'text/html');
        return { nextDocument, responseUrl };
    }

    function getPrefetchedDocument(url) {
        const key = url.href;
        const cached = prefetchedDocuments.get(key);
        if (!cached || Date.now() - cached.createdAt > PREFETCH_TTL_MS) {
            prefetchedDocuments.delete(key);
            return null;
        }
        return cached.promise;
    }

    function prefetch(url) {
        if (getPrefetchedDocument(url)) {
            return;
        }

        const promise = fetchPageDocument(url)
            .catch((error) => {
                prefetchedDocuments.delete(url.href);
                throw error;
            });
        prefetchedDocuments.set(url.href, {
            createdAt: Date.now(),
            promise
        });
        promise.catch(() => {});
    }

    function invalidatePrefetches() {
        prefetchedDocuments.clear();
    }

    function copyElementAttributes(target, source) {
        Array.from(target.attributes).forEach((attribute) => target.removeAttribute(attribute.name));
        Array.from(source.attributes).forEach((attribute) => target.setAttribute(attribute.name, attribute.value));
    }

    function syncAntiforgeryToken(nextDocument) {
        const currentHost = document.getElementById('global-antiforgery-token');
        const nextHost = nextDocument.getElementById('global-antiforgery-token');
        if (!currentHost || !nextHost) {
            return;
        }

        currentHost.replaceChildren(...Array.from(nextHost.childNodes).map((node) => document.importNode(node, true)));
    }

    function syncChromeContext(nextDocument) {
        const currentContext = document.getElementById('layout-chrome-context');
        const nextContext = nextDocument.getElementById('layout-chrome-context');
        if (currentContext && nextContext) {
            copyElementAttributes(currentContext, nextContext);
        }

        const currentNavigation = document.getElementById('chrome-navigation');
        const nextNavigation = nextDocument.getElementById('chrome-navigation');
        if (!currentNavigation || !nextNavigation) {
            return;
        }

        const activeTabs = new Set(Array.from(nextNavigation.querySelectorAll('[data-tab].active'))
            .map((item) => item.dataset.tab || '')
            .filter(Boolean));
        currentNavigation.querySelectorAll('[data-tab]').forEach((item) => {
            item.classList.toggle('active', activeTabs.has(item.dataset.tab || ''));
        });
    }

    function replacePageContent(nextDocument) {
        const currentContent = document.getElementById('content_admin');
        const nextContent = nextDocument.getElementById('content_admin');
        if (!currentContent || !nextContent) {
            return null;
        }

        window.AppPageLifecycle?.unmount?.(currentContent);
        currentContent.replaceChildren(...Array.from(nextContent.childNodes)
            .map((node) => document.importNode(node, true)));
        return currentContent;
    }

    function appendImportedNode(host, node) {
        const importedNode = document.importNode(node, true);
        host.appendChild(importedNode);
        return importedNode;
    }

    async function executePageScripts(nextDocument, baseUrl) {
        const currentHost = document.getElementById(PAGE_SCRIPT_HOST);
        const nextHost = nextDocument.getElementById(PAGE_SCRIPT_HOST);
        if (!currentHost) {
            return;
        }

        currentHost.replaceChildren();
        if (!nextHost) {
            return;
        }

        for (const sourceNode of Array.from(nextHost.childNodes)) {
            if (sourceNode.nodeType !== Node.ELEMENT_NODE || sourceNode.tagName.toLowerCase() !== 'script') {
                appendImportedNode(currentHost, sourceNode);
                continue;
            }

            if (!isExecutableScript(sourceNode)) {
                appendImportedNode(currentHost, sourceNode);
                continue;
            }

            const sourceUrl = sourceNode.getAttribute('src');
            if (sourceUrl) {
                const absoluteUrl = getAbsoluteAssetUrl(sourceUrl, baseUrl);
                if (!absoluteUrl || loadedScriptUrls.has(absoluteUrl)) {
                    continue;
                }

                const script = document.createElement('script');
                Array.from(sourceNode.attributes).forEach((attribute) => {
                    if (attribute.name !== 'src') {
                        script.setAttribute(attribute.name, attribute.value);
                    }
                });
                script.src = absoluteUrl;
                await new Promise((resolve, reject) => {
                    script.addEventListener('load', resolve, { once: true });
                    script.addEventListener('error', () => reject(new Error(`Не удалось загрузить сценарий ${absoluteUrl}`)), { once: true });
                    currentHost.appendChild(script);
                });
                loadedScriptUrls.add(absoluteUrl);
                continue;
            }

            const script = document.createElement('script');
            Array.from(sourceNode.attributes).forEach((attribute) => script.setAttribute(attribute.name, attribute.value));
            script.textContent = sourceNode.textContent || '';
            currentHost.appendChild(script);
        }
    }

    function setHistory(url, mode) {
        if (mode === 'none') {
            return;
        }

        const currentState = window.history.state && typeof window.history.state === 'object'
            ? window.history.state
            : {};
        const nextState = {
            ...currentState,
            [NAVIGATION_STATE_KEY]: true
        };
        const path = getDocumentPath(url);
        if (mode === 'replace') {
            window.history.replaceState(nextState, document.title, path);
        } else {
            window.history.pushState(nextState, document.title, path);
        }
    }

    function restoreScroll(url, mode) {
        if (mode === 'restore') {
            window.AppScrollState?.restorePosition?.(`${url.pathname}${url.search}`);
            return;
        }

        if (mode === 'top') {
            const scroller = document.getElementById('content_admin');
            if (scroller) {
                scroller.scrollTop = 0;
            } else {
                window.scrollTo(0, 0);
            }
            return;
        }

        window.AppScrollState?.restoreCurrentPosition?.({ preferCarry: true });
    }

    function setPending(isPending) {
        document.body?.classList.toggle('app-navigation-pending', isPending);
        const content = document.getElementById('content_admin');
        if (content) {
            content.toggleAttribute('aria-busy', isPending);
        }
    }

    function hardNavigate(url, mode = 'assign') {
        if (mode === 'replace') {
            window.location.replace(url.href);
        } else {
            window.location.assign(url.href);
        }
    }

    async function navigate(value, options = {}) {
        const requestedUrl = toUrl(value);
        if (!requestedUrl || requestedUrl.origin !== window.location.origin) {
            if (requestedUrl) {
                hardNavigate(requestedUrl, options.historyMode);
            }
            return false;
        }

        const currentShell = document.querySelector('[data-app-shell]');
        if (!currentShell) {
            hardNavigate(requestedUrl, options.historyMode);
            return false;
        }

        const sequence = ++navigationSequence;
        activeController?.abort();
        activeController = new AbortController();
        const scrollMode = options.scrollMode || 'carry';
        if (options.historyMode !== 'none') {
            window.AppScrollState?.prepareNavigation?.({ carry: scrollMode === 'carry' });
        }
        setPending(true);

        try {
            const prefetched = getPrefetchedDocument(requestedUrl);
            if (prefetched) {
                prefetchedDocuments.delete(requestedUrl.href);
            }
            const { nextDocument, responseUrl } = prefetched
                ? await prefetched
                : await fetchPageDocument(requestedUrl, activeController.signal);
            if (sequence !== navigationSequence) {
                return false;
            }

            const nextShell = nextDocument.querySelector('[data-app-shell]');
            const nextContent = nextDocument.getElementById('content_admin');
            if (!nextShell || !nextContent || nextShell.dataset.appShell !== currentShell.dataset.appShell) {
                hardNavigate(responseUrl, options.historyMode);
                return false;
            }

            await installPageStyles(nextDocument, responseUrl.href);
            if (sequence !== navigationSequence) {
                return false;
            }

            const content = replacePageContent(nextDocument);
            if (!content) {
                hardNavigate(responseUrl, options.historyMode);
                return false;
            }

            document.title = nextDocument.title || document.title;
            syncAntiforgeryToken(nextDocument);
            syncChromeContext(nextDocument);
            setHistory(responseUrl, options.historyMode || 'push');
            await executePageScripts(nextDocument, responseUrl.href);
            window.AppDate?.enhanceDateInputs?.(content);
            window.AppPassword?.mount?.(content);
            window.AppPageLifecycle?.mount?.(content);
            window.showPendingAdminNotification?.();
            window.queueNavigationLayoutEvaluation?.();
            restoreScroll(responseUrl, options.historyMode === 'none' ? 'restore' : scrollMode);
            document.dispatchEvent(new CustomEvent('app:navigation-complete', {
                detail: { url: responseUrl.href, content }
            }));
            return true;
        } catch (error) {
            if (error?.name === 'AbortError') {
                return false;
            }

            if (error?.requiresFullNavigation) {
                hardNavigate(requestedUrl, options.historyMode);
                return false;
            }

            console.error('Ошибка частичной навигации:', error);
            window.AppUi?.notify?.(
                window.normalizeClientErrorMessage?.(error?.message, 'Не удалось открыть страницу.')
                    || 'Не удалось открыть страницу.',
                'error'
            );
            return false;
        } finally {
            if (sequence === navigationSequence) {
                activeController = null;
                setPending(false);
            }
        }
    }

    function isModifiedClick(event) {
        return event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;
    }

    function getEligibleLink(target) {
        const link = target?.closest?.('a[href]');
        if (!link
            || link.hasAttribute('download')
            || link.dataset.noAppNavigation === 'true'
            || (link.target && link.target !== '_self')) {
            return null;
        }

        const rawHref = (link.getAttribute('href') || '').trim();
        if (!rawHref
            || rawHref.startsWith('#')
            || rawHref.startsWith('javascript:')
            || rawHref.startsWith('mailto:')
            || rawHref.startsWith('tel:')) {
            return null;
        }

        const url = toUrl(link.href);
        if (!url || url.origin !== window.location.origin) {
            return null;
        }
        return { link, url };
    }

    function handleDocumentClick(event) {
        if (event.defaultPrevented || isModifiedClick(event)) {
            return;
        }

        const target = getEligibleLink(event.target);
        if (!target) {
            return;
        }

        const currentUrl = toUrl(window.location.href);
        if (currentUrl
            && target.url.pathname === currentUrl.pathname
            && target.url.search === currentUrl.search
            && target.url.hash) {
            return;
        }

        event.preventDefault();
        navigate(target.url, { historyMode: 'push', scrollMode: 'carry' });
    }

    function schedulePrefetch(event) {
        const target = getEligibleLink(event.target);
        if (!target || prefetchTimers.has(target.link)) {
            return;
        }

        const timerId = window.setTimeout(() => {
            prefetchTimers.delete(target.link);
            if (!target.link.isConnected || activeController) {
                return;
            }
            prefetch(target.url);
        }, 80);
        prefetchTimers.set(target.link, timerId);
    }

    function cancelPrefetch(event) {
        const link = event.target?.closest?.('a[href]');
        const timerId = link ? prefetchTimers.get(link) : null;
        if (!timerId) {
            return;
        }
        window.clearTimeout(timerId);
        prefetchTimers.delete(link);
    }

    function handlePopState(event) {
        const isClientTabState = Boolean(event.state?.tab)
            && document.querySelector('[data-page="user-surveys"]');
        if (isClientTabState && event.state?.[NAVIGATION_STATE_KEY] !== true) {
            return;
        }

        navigate(window.location.href, {
            historyMode: 'none',
            scrollMode: 'restore'
        });
    }

    function initialize() {
        rememberLoadedScripts();
        markInitialPageStyles();
        const initialState = window.history.state && typeof window.history.state === 'object'
            ? window.history.state
            : {};
        window.history.replaceState({
            ...initialState,
            [NAVIGATION_STATE_KEY]: true
        }, document.title, window.location.href);
    }

    window.AppNavigationRouter = {
        navigate,
        prefetch,
        invalidatePrefetches
    };

    document.addEventListener('click', handleDocumentClick);
    document.addEventListener('pointerover', schedulePrefetch);
    document.addEventListener('pointerout', cancelPrefetch);
    document.addEventListener('focusin', schedulePrefetch);
    window.addEventListener('popstate', handlePopState);

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
    } else {
        initialize();
    }
})();
