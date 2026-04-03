(function (window) {
    'use strict';

    const AppCore = window.AppCore || {};

    function init() {
        const root = document.getElementById('root');
        if (!root) return;

        root.setAttribute('data-admin-foundation', 'ready');
        window.AdminFoundation = {
            version: 'stage1',
            rootId: 'root',
            hasDomUtils: !!AppCore.dom,
            hasHttp: !!AppCore.http,
            hasModal: !!AppCore.modal
        };
    }

    if (AppCore.dom && typeof AppCore.dom.ready === 'function') {
        AppCore.dom.ready(init);
        return;
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
        return;
    }

    init();
})(window);
