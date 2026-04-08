(function () {
    function parseArgs(rawValue) {
        if (!rawValue) {
            return [];
        }

        try {
            const parsed = JSON.parse(rawValue);
            return Array.isArray(parsed) ? parsed : [parsed];
        } catch (error) {
            console.warn('Не удалось разобрать data-args:', rawValue, error);
            return [];
        }
    }

    function resolveFunction(path) {
        return path.split('.').reduce((current, key) => current?.[key], window);
    }

    function invokeFunction(path, args) {
        const target = resolveFunction(path);
        if (typeof target !== 'function') {
            console.warn(`Функция ${path} не найдена`);
            return false;
        }

        target.apply(window, args);
        return true;
    }

    function closeModalById(modalId) {
        if (!modalId) {
            return;
        }

        const modal = document.getElementById(modalId);
        if (!modal) {
            return;
        }

        if (typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(modal);
            return;
        }

        modal.style.display = 'none';
    }

    function navigateByTab(tabName, fallbackUrl) {
        if (tabName && typeof window.handleTabClick === 'function') {
            window.handleTabClick(tabName);
            return;
        }

        if (fallbackUrl) {
            window.location.assign(fallbackUrl);
        }
    }

    function buildArgs(element, event, prefix) {
        const args = parseArgs(element.dataset[`${prefix}Args`]);

        if (element.dataset[`${prefix}PassElement`] === 'true') {
            args.push(element);
        }

        if (element.dataset[`${prefix}PassEvent`] === 'true') {
            args.push(event);
        }

        return args;
    }

    function handleConfiguredCall(element, event, prefix) {
        const functionName = element.dataset[`${prefix}Call`];
        if (!functionName) {
            return false;
        }

        if (element.dataset[`${prefix}PreventDefault`] === 'true' || element.tagName === 'A') {
            event.preventDefault();
        }

        const wasCalled = invokeFunction(functionName, buildArgs(element, event, prefix));
        if (!wasCalled && prefix === 'click' && element.dataset.fallbackUrl) {
            window.location.assign(element.dataset.fallbackUrl);
        }

        return true;
    }

    document.addEventListener('click', function (event) {
        const element = event.target.closest('[data-click-call], [data-modal-close], [data-tab-target], [data-redirect-url]');
        if (!element) {
            return;
        }

        if (handleConfiguredCall(element, event, 'click')) {
            return;
        }

        if (element.dataset.modalClose) {
            event.preventDefault();
            closeModalById(element.dataset.modalClose);
            return;
        }

        if (element.dataset.tabTarget) {
            event.preventDefault();
            navigateByTab(element.dataset.tabTarget, element.dataset.fallbackUrl || element.getAttribute('href') || '');
            return;
        }

        if (element.dataset.redirectUrl) {
            event.preventDefault();
            window.location.assign(element.dataset.redirectUrl);
        }
    });

    document.addEventListener('change', function (event) {
        const element = event.target.closest('[data-change-call]');
        if (!element) {
            return;
        }

        handleConfiguredCall(element, event, 'change');
    });

    document.addEventListener('focusin', function (event) {
        const element = event.target.closest('[data-focus-call]');
        if (!element) {
            return;
        }

        handleConfiguredCall(element, event, 'focus');
    });
})();
