(function () {
    const SVG_NS = 'http://www.w3.org/2000/svg';

    function createPasswordEye(iconClass, paths, circle) {
        const svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('class', iconClass);
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');

        paths.forEach((pathData) => {
            const path = document.createElementNS(SVG_NS, 'path');
            path.setAttribute('d', pathData);
            svg.appendChild(path);
        });

        if (circle) {
            const circleElement = document.createElementNS(SVG_NS, 'circle');
            circleElement.setAttribute('cx', String(circle.cx));
            circleElement.setAttribute('cy', String(circle.cy));
            circleElement.setAttribute('r', String(circle.r));
            svg.appendChild(circleElement);
        }

        return svg;
    }

    function renderPasswordEye(button, isVisible) {
        button.replaceChildren(isVisible
            ? createPasswordEye('eye-closed', [
                'M3 3l18 18',
                'M10.6 10.7a3 3 0 0 0 4 4',
                'M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8',
                'M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3'
            ])
            : createPasswordEye(
                'eye-open',
                ['M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z'],
                { cx: 12, cy: 12, r: 3 }
            ));
    }

    function setPasswordVisibility(input, button, isVisible) {
        input.dataset.passwordVisible = isVisible ? 'true' : 'false';
        input.classList.toggle('is-password-masked', !isVisible);
        button.classList.toggle('is-visible', isVisible);

        const label = isVisible ? 'Скрыть пароль' : 'Показать пароль';
        button.setAttribute('aria-label', label);
        button.setAttribute('title', label);
        renderPasswordEye(button, isVisible);
    }

    function mountPasswordField(input) {
        if (!input || input.dataset.eyeApplied === 'true') {
            return false;
        }

        const button = input
            .closest('.app-field-with-icon.has-toggle')
            ?.querySelector('.password-toggle-btn');
        if (!button) {
            return false;
        }

        button.addEventListener('mousedown', (event) => event.preventDefault());
        button.addEventListener('click', () => {
            const nextVisible = input.dataset.passwordVisible !== 'true';
            setPasswordVisibility(input, button, nextVisible);
            window.requestAnimationFrame(() => {
                input.focus({ preventScroll: true });
                const valueLength = input.value?.length || 0;
                input.setSelectionRange?.(valueLength, valueLength);
            });
        });

        setPasswordVisibility(input, button, input.dataset.passwordVisible === 'true');
        input.dataset.eyeApplied = 'true';
        return true;
    }

    function mountPasswordFields(root = document) {
        root.querySelectorAll?.('input[data-password-field="true"]')
            .forEach(mountPasswordField);
    }

    window.AppPassword = {
        mountField: mountPasswordField,
        mount: mountPasswordFields
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => mountPasswordFields(), { once: true });
    } else {
        mountPasswordFields();
    }

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

        if (window.AppUi?.setModalVisibility) {
            window.AppUi.setModalVisibility(modal, false);
            return;
        }

        if (typeof window.hideSiteModal === 'function') {
            window.hideSiteModal(modal);
            return;
        }
    }

    function navigateByTab(tabName, fallbackUrl) {
        if (tabName && typeof window.refreshAdminUi === 'function') {
            window.refreshAdminUi({ tabName, fallbackUrl });
            return;
        }

        if (fallbackUrl) {
            window.AppScrollState?.prepareNavigation({ carry: true });
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
            window.AppScrollState?.prepareNavigation({ carry: true });
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
