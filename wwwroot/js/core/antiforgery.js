(function () {
    const headerName = 'RequestVerificationToken';
    const ajaxHeaderName = 'X-Requested-With';
    const ajaxHeaderValue = 'XMLHttpRequest';
    const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

    function getRequestVerificationToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function isUnsafeMethod(method) {
        return unsafeMethods.has((method || 'GET').toUpperCase());
    }

    function isSameOrigin(url) {
        try {
            return new URL(url, window.location.href).origin === window.location.origin;
        } catch {
            return false;
        }
    }

    function redirectUnauthenticatedUser(response) {
        const wasBlocked = response.status === 401
            && response.headers.get('X-Authentication-Status') === 'blocked';
        const responseUrl = new URL(response.url, window.location.href);
        const redirectedToBlockedLogin = response.redirected
            && responseUrl.searchParams.get('auth') === 'blocked';

        if (wasBlocked || redirectedToBlockedLogin) {
            window.location.assign('/?auth=blocked');
            return response;
        }

        const redirectedToLogin = response.redirected
            && responseUrl.origin === window.location.origin
            && responseUrl.pathname === '/';
        const isLoginPage = window.location.pathname === '/';
        if ((response.status === 401 && !isLoginPage) || redirectedToLogin) {
            window.location.assign('/');
        }

        return response;
    }

    function sendFetch(input, init) {
        return originalFetch(input, init).then(redirectUnauthenticatedUser);
    }

    const originalFetch = window.fetch?.bind(window);
    if (originalFetch) {
        window.fetch = function (input, init) {
            const request = input instanceof Request ? input : null;
            const method = (init?.method || request?.method || 'GET').toUpperCase();
            const url = typeof input === 'string' ? input : request?.url || window.location.href;

            if (!isSameOrigin(url)) {
                return sendFetch(input, init);
            }

            const token = getRequestVerificationToken();
            if (request && !init) {
                const headers = new Headers(request.headers);
                if (isUnsafeMethod(method) && token && !headers.has(headerName)) {
                    headers.set(headerName, token);
                }
                if (!headers.has(ajaxHeaderName)) {
                    headers.set(ajaxHeaderName, ajaxHeaderValue);
                }
                input = new Request(request, { headers });

                return sendFetch(input);
            }

            const headers = new Headers(init?.headers || request?.headers || undefined);
            if (isUnsafeMethod(method) && token && !headers.has(headerName)) {
                headers.set(headerName, token);
            }
            if (!headers.has(ajaxHeaderName)) {
                headers.set(ajaxHeaderName, ajaxHeaderValue);
            }

            return sendFetch(input, { ...init, headers });
        };
    }

    if (!window.XMLHttpRequest) {
        return;
    }

    const originalOpen = XMLHttpRequest.prototype.open;
    const originalSend = XMLHttpRequest.prototype.send;
    const originalSetRequestHeader = XMLHttpRequest.prototype.setRequestHeader;

    XMLHttpRequest.prototype.open = function (method, url) {
        this.__csrfMethod = typeof method === 'string' ? method.toUpperCase() : 'GET';
        this.__csrfUrl = typeof url === 'string' ? url : window.location.href;
        this.__csrfHeaders = new Set();

        return originalOpen.apply(this, arguments);
    };

    XMLHttpRequest.prototype.setRequestHeader = function (name, value) {
        if (typeof name === 'string') {
            if (!this.__csrfHeaders) {
                this.__csrfHeaders = new Set();
            }

            this.__csrfHeaders.add(name.toLowerCase());
        }

        return originalSetRequestHeader.call(this, name, value);
    };

    XMLHttpRequest.prototype.send = function (body) {
        const method = this.__csrfMethod || 'GET';
        const url = this.__csrfUrl || window.location.href;
        const hasAntiforgeryHeader = this.__csrfHeaders?.has(headerName.toLowerCase());
        const hasAjaxHeader = this.__csrfHeaders?.has(ajaxHeaderName.toLowerCase());

        if (isUnsafeMethod(method) && isSameOrigin(url) && !hasAntiforgeryHeader) {
            const token = getRequestVerificationToken();
            if (token) {
                originalSetRequestHeader.call(this, headerName, token);
            }
        }
        if (isSameOrigin(url) && !hasAjaxHeader) {
            originalSetRequestHeader.call(this, ajaxHeaderName, ajaxHeaderValue);
        }

        return originalSend.call(this, body);
    };
})();
