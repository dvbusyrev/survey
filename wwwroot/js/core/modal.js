(function (window) {
    'use strict';

    function setDisplay(element, value) {
        if (!element) return;
        element.style.display = value;
    }

    window.AppCore = window.AppCore || {};
    window.AppCore.modal = {
        open(elementOrId, displayMode) {
            const element = typeof elementOrId === 'string'
                ? document.getElementById(elementOrId)
                : elementOrId;
            setDisplay(element, displayMode || 'block');
            return element;
        },
        close(elementOrId) {
            const element = typeof elementOrId === 'string'
                ? document.getElementById(elementOrId)
                : elementOrId;
            setDisplay(element, 'none');
            return element;
        }
    };
})(window);
