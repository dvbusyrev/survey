(function () {
    const headerName = 'RequestVerificationToken';
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

    const originalFetch = window.fetch?.bind(window);
    if (originalFetch) {
        window.fetch = function (input, init) {
            const request = input instanceof Request ? input : null;
            const method = (init?.method || request?.method || 'GET').toUpperCase();
            const url = typeof input === 'string' ? input : request?.url || window.location.href;

            if (!isUnsafeMethod(method) || !isSameOrigin(url)) {
                return originalFetch(input, init);
            }

            const token = getRequestVerificationToken();
            if (!token) {
                return originalFetch(input, init);
            }

            if (request && !init) {
                const headers = new Headers(request.headers);
                if (!headers.has(headerName)) {
                    headers.set(headerName, token);
                    input = new Request(request, { headers });
                }

                return originalFetch(input);
            }

            const headers = new Headers(init?.headers || request?.headers || undefined);
            if (!headers.has(headerName)) {
                headers.set(headerName, token);
            }

            return originalFetch(input, { ...init, headers });
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
        const hasHeader = this.__csrfHeaders?.has(headerName.toLowerCase());

        if (isUnsafeMethod(method) && isSameOrigin(url) && !hasHeader) {
            const token = getRequestVerificationToken();
            if (token) {
                originalSetRequestHeader.call(this, headerName, token);
            }
        }

        return originalSend.call(this, body);
    };
})();
