(() => {
    const DEFAULT_MIN_HEIGHT = 160;
    const DEFAULT_BOTTOM_GAP = 24;

    function resolveList(container, selector) {
        if (!container || typeof container.querySelector !== 'function') {
            return null;
        }

        return container.querySelector(selector || '.app-checkbox-list');
    }

    function updateListHeight(container, options = {}) {
        const list = resolveList(container, options.selector);
        if (!list) {
            return false;
        }

        const minimumHeight = Number(options.minimumHeight) || DEFAULT_MIN_HEIGHT;
        const bottomGap = Number(options.bottomGap) || DEFAULT_BOTTOM_GAP;
        const availableHeight = Math.max(
            minimumHeight,
            window.innerHeight - list.getBoundingClientRect().top - bottomGap
        );

        list.style.setProperty('--app-checkbox-list-max-height', `${availableHeight}px`);
        return true;
    }

    function scheduleListHeightUpdate(container, options) {
        window.requestAnimationFrame(() => updateListHeight(container, options));
    }

    window.AppCheckboxDropdown = {
        ...(window.AppCheckboxDropdown || {}),
        updateListHeight,
        scheduleListHeightUpdate
    };
})();
