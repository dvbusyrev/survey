    const DETACHED_CONTENT_HOST_ID = 'admin-inline-detached-content';
    const loadedStylesheetUrls = new Set();
    const loadedScriptUrls = new Set();
    let loadedAssetsPrimed = false;
    function parseHtmlDocument(html) {
        const parser = new DOMParser();
        return parser.parseFromString(html || '', 'text/html');
    }

    function normalizeAssetUrl(url) {
        if (!url) {
            return '';
        }

        try {
            return new URL(url, window.location.origin).href;
        } catch (error) {
            return '';
        }
    }

    function primeLoadedAssets() {
        if (loadedAssetsPrimed) {
            return;
        }

        document.querySelectorAll('link[rel="stylesheet"][href]').forEach((link) => {
            const href = normalizeAssetUrl(link.href);
            if (href) {
                loadedStylesheetUrls.add(href);
            }
        });

        document.querySelectorAll('script[src]').forEach((script) => {
            const src = normalizeAssetUrl(script.src);
            if (src) {
                loadedScriptUrls.add(src);
            }
        });

        loadedAssetsPrimed = true;
    }

    function isThemeStylesheetUrl(href) {
        try {
            return new URL(href, window.location.origin).pathname.endsWith('/css/shared/app-theme.css');
        } catch (error) {
            return false;
        }
    }

    function getThemeStylesheetAnchor() {
        return Array.from(document.querySelectorAll('link[rel="stylesheet"][href]'))
            .find((link) => isThemeStylesheetUrl(link.getAttribute('href') || link.href))
            || document.getElementById('app-theme-inline');
    }

    function normalizeThemeStylesheetOrder() {
        const themeAnchor = getThemeStylesheetAnchor();
        const head = themeAnchor?.parentNode;
        if (!head) {
            return;
        }

        const children = Array.from(head.children);
        const themeIndex = children.indexOf(themeAnchor);
        if (themeIndex < 0) {
            return;
        }

        children.slice(themeIndex + 1).forEach((node) => {
            if (
                node.tagName === 'LINK'
                && node.getAttribute('rel') === 'stylesheet'
                && !isThemeStylesheetUrl(node.getAttribute('href') || node.href)
            ) {
                head.insertBefore(node, themeAnchor);
            }
        });
    }

    function insertStylesheetBeforeTheme(link) {
        normalizeThemeStylesheetOrder();

        const themeAnchor = getThemeStylesheetAnchor();
        if (themeAnchor?.parentNode) {
            themeAnchor.parentNode.insertBefore(link, themeAnchor);
            return;
        }

        document.head.appendChild(link);
    }

    function loadStylesheetsFromDocument(parsedDocument) {
        primeLoadedAssets();
        normalizeThemeStylesheetOrder();

        parsedDocument.querySelectorAll('link[rel="stylesheet"][href]').forEach((sourceLink) => {
            const href = normalizeAssetUrl(sourceLink.getAttribute('href'));
            if (!href || loadedStylesheetUrls.has(href)) {
                return;
            }

            loadedStylesheetUrls.add(href);
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;

            if (sourceLink.media) {
                link.media = sourceLink.media;
            }

            insertStylesheetBeforeTheme(link);
        });
    }

    function loadScriptAsset(src) {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = src;
            script.async = false;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Не удалось загрузить скрипт: ${src}`));
            document.body.appendChild(script);
        });
    }

    async function loadScriptsFromDocument(parsedDocument) {
        primeLoadedAssets();
        let loadedAnyScript = false;
        const scriptSources = Array.from(parsedDocument.querySelectorAll('script[src]'))
            .map((script) => normalizeAssetUrl(script.getAttribute('src')))
            .filter(Boolean)
            .filter((src, index, list) => list.indexOf(src) === index);

        for (const src of scriptSources) {
            if (loadedScriptUrls.has(src)) {
                continue;
            }

            loadedScriptUrls.add(src);
            try {
                await loadScriptAsset(src);
                loadedAnyScript = true;
            } catch (error) {
                loadedScriptUrls.delete(src);
                throw error;
            }
        }

        return loadedAnyScript;
    }

    function shouldSkipFetchedNode(node) {
        if (!node) {
            return true;
        }

        if (node.nodeType === Node.TEXT_NODE) {
            return !node.textContent.trim();
        }

        if (node.nodeType !== Node.ELEMENT_NODE) {
            return false;
        }

        const element = node;
        if (['SCRIPT', 'LINK', 'STYLE', 'META', 'TITLE'].includes(element.tagName)) {
            return true;
        }

        if ([
            'global-antiforgery-token',
            'layout-chrome-context',
            'chrome-context',
            'chrome-header',
            'chrome-navigation',
            'chrome-footer',
            'app-theme-background',
            'app-theme-effects-root',
            'app-theme-foreground-effects-root',
            'root',
            DETACHED_CONTENT_HOST_ID
        ].includes(element.id)) {
            return true;
        }

        if (element.tagName === 'TEMPLATE' && ['nav-template-admin', 'nav-template-user', 'header-template', 'footer-template', 'admin-extension-modal-template', 'admin-extension-modal-row-template', 'admin-statistics-template'].includes(element.id)) {
            return true;
        }

        if (element.querySelector && element.querySelector('#content_admin')) {
            return true;
        }

        return false;
    }

    function getPrimaryRenderableNodes(sourceDocument) {
        const contentHost = sourceDocument.getElementById('content_admin');
        if (contentHost) {
            return Array.from(contentHost.childNodes);
        }

        const pageContent = sourceDocument.getElementById('default_content');
        return pageContent
            ? [pageContent]
            : Array.from(sourceDocument.body.childNodes);
    }

    function getDetachedRenderableNodes(sourceDocument) {
        const contentHost = sourceDocument.getElementById('content_admin');
        const pageContent = sourceDocument.getElementById('default_content');
        const primaryNode = contentHost || pageContent;
        if (!primaryNode) {
            return [];
        }

        const nodes = [];
        const seen = new Set();
        const detachedSelectors = [
            '.modal',
            '[id$="Modal"]',
            'template',
            '#notification',
            '#loadingOverlay',
            '#survey-edit-selected-organization-names'
        ];

        const appendNode = (node) => {
            if (!node || seen.has(node) || node === primaryNode || shouldSkipFetchedNode(node)) {
                return;
            }

            if (primaryNode.contains?.(node)) {
                return;
            }

            if (
                node.nodeType === Node.ELEMENT_NODE
                && (node.querySelector?.('#content_admin') || node.querySelector?.('#default_content'))
            ) {
                return;
            }

            seen.add(node);
            nodes.push(node);
        };

        Array.from(sourceDocument.body.childNodes).forEach(appendNode);
        sourceDocument.body
            .querySelectorAll(detachedSelectors.join(','))
            .forEach(appendNode);

        return nodes;
    }

    function buildFragmentFromNodes(nodes, cloneNodes = true) {
        const fragment = document.createDocumentFragment();

        (nodes || []).forEach((node) => {
            if (!shouldSkipFetchedNode(node)) {
                fragment.appendChild(cloneNodes ? node.cloneNode(true) : node);
            }
        });

        return fragment;
    }

    function buildRenderableFragment(parsedDocument) {
        return buildFragmentFromNodes(getPrimaryRenderableNodes(parsedDocument));
    }

    function ensureDetachedContentHost() {
        let host = document.getElementById(DETACHED_CONTENT_HOST_ID);
        if (host) {
            return host;
        }

        host = document.createElement('div');
        host.id = DETACHED_CONTENT_HOST_ID;
        document.body.appendChild(host);
        return host;
    }

    function syncDetachedContent(sourceDocument, cloneNodes = true) {
        const host = ensureDetachedContentHost();
        host.innerHTML = '';
        host.appendChild(buildFragmentFromNodes(getDetachedRenderableNodes(sourceDocument), cloneNodes));
    }

    function captureInitialDetachedContent() {
        if (!document.body) {
            return;
        }

        syncDetachedContent(document, false);
    }

    function hydrateFetchedContentState() {
        const selectedOrganizationNamesElement = document.getElementById('survey-edit-selected-organization-names');
        if (!selectedOrganizationNamesElement) {
            window.selectedOrganizationNames = [];
            return;
        }

        try {
            window.selectedOrganizationNames = JSON.parse(selectedOrganizationNamesElement.content.textContent.trim());
        } catch (error) {
            console.warn('Не удалось восстановить выбранные организации из шаблона.', error);
            window.selectedOrganizationNames = [];
        }
    }

export {
    parseHtmlDocument,
    loadStylesheetsFromDocument,
    loadScriptsFromDocument,
    buildRenderableFragment,
    syncDetachedContent,
    captureInitialDetachedContent,
    hydrateFetchedContentState
};
