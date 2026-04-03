(function (window) {
    'use strict';

    const DomUtils = {
        byId(id) {
            return document.getElementById(id);
        },
        qs(selector, root) {
            return (root || document).querySelector(selector);
        },
        qsa(selector, root) {
            return Array.from((root || document).querySelectorAll(selector));
        },
        on(target, eventName, handler, options) {
            if (!target || !eventName || typeof handler !== 'function') return;
            target.addEventListener(eventName, handler, options || false);
        },
        ready(callback) {
            if (typeof callback !== 'function') return;
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', callback, { once: true });
                return;
            }
            callback();
        },
        toggleClass(target, className, force) {
            if (!target || !className) return false;
            return target.classList.toggle(className, force);
        }
    };

    window.AppCore = window.AppCore || {};
    window.AppCore.dom = DomUtils;
})(window);
