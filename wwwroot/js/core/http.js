(function (window) {
    'use strict';

    async function requestJson(url, options) {
        const response = await fetch(url, options || {});
        const contentType = response.headers.get('content-type') || '';
        const isJson = contentType.includes('application/json');
        const payload = isJson ? await response.json() : await response.text();

        if (!response.ok) {
            const error = new Error(typeof payload === 'string' ? payload : 'HTTP request failed');
            error.status = response.status;
            error.payload = payload;
            throw error;
        }

        return payload;
    }

    window.AppCore = window.AppCore || {};
    window.AppCore.http = {
        getJson(url, options) {
            return requestJson(url, Object.assign({ method: 'GET' }, options));
        },
        postJson(url, body, options) {
            return requestJson(url, Object.assign({
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            }, options));
        }
    };
})(window);
