(function () {
    if (window.__adminPasswordToolsLoaded) {
        return;
    }

    window.__adminPasswordToolsLoaded = true;
    const SVG_NS = 'http://www.w3.org/2000/svg';

    function createSvgEye(iconClass, paths, circle) {
        const svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('class', iconClass);
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');

        paths.forEach(function (d) {
            const path = document.createElementNS(SVG_NS, 'path');
            path.setAttribute('d', d);
            svg.appendChild(path);
        });

        if (circle) {
            const circleEl = document.createElementNS(SVG_NS, 'circle');
            circleEl.setAttribute('cx', String(circle.cx));
            circleEl.setAttribute('cy', String(circle.cy));
            circleEl.setAttribute('r', String(circle.r));
            svg.appendChild(circleEl);
        }

        return svg;
    }

    function setPasswordVisibility(input, btn, isVisible) {
        if (!input) return;

        input.dataset.passwordVisible = isVisible ? 'true' : 'false';
        input.classList.toggle('is-password-masked', !isVisible);

        if (btn) {
            btn.classList.toggle('is-visible', isVisible);
            btn.setAttribute('aria-label', isVisible ? 'Скрыть пароль' : 'Показать пароль');
            btn.setAttribute('title', isVisible ? 'Скрыть пароль' : 'Показать пароль');
        }
    }

    function renderPasswordEye(btn, isVisible) {
        if (!btn) return;

        btn.textContent = '';
        if (isVisible) {
            btn.appendChild(createSvgEye('eye-closed', [
                'M3 3l18 18',
                'M10.6 10.7a3 3 0 0 0 4 4',
                'M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8',
                'M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3'
            ]));
            return;
        }

        btn.appendChild(createSvgEye('eye-open', ['M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z'], { cx: 12, cy: 12, r: 3 }));
    }

    function attachInlinePasswordToggle(input, btn) {
        if (!input || !btn) return false;

        btn.addEventListener('mousedown', function (event) {
            event.preventDefault();
        });

        btn.addEventListener('click', function () {
            const isVisible = input.dataset.passwordVisible === 'true';
            const nextVisible = !isVisible;
            setPasswordVisibility(input, btn, nextVisible);
            renderPasswordEye(btn, nextVisible);

            window.requestAnimationFrame(function () {
                input.focus({ preventScroll: true });
                const length = input.value ? input.value.length : 0;
                if (typeof input.setSelectionRange === 'function') {
                    input.setSelectionRange(length, length);
                }
            });
        });

        const isVisible = input.dataset.passwordVisible === 'true';
        setPasswordVisibility(input, btn, isVisible);
        renderPasswordEye(btn, isVisible);
        input.dataset.eyeApplied = 'true';
        return true;
    }

    function addPasswordEye(input) {
        if (window.AppPassword?.mountField) {
            return window.AppPassword.mountField(input);
        }

        if (!input || input.dataset.eyeApplied === 'true') return;
        const inlineToggle = input
            .closest('.app-field-with-icon.has-toggle')
            ?.querySelector('.password-toggle-btn');
        if (attachInlinePasswordToggle(input, inlineToggle)) {
            return;
        }

        if (input.closest('.password-eye-wrap')) {
            input.dataset.eyeApplied = 'true';
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'password-eye-wrap';

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const btn = window.AppUi.createElement('button', {
            type: 'button',
            className: 'password-eye-btn',
            ariaLabel: 'Показать пароль'
        });
        btn.appendChild(createSvgEye('eye-open', ['M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z'], { cx: 12, cy: 12, r: 3 }));
        btn.appendChild(createSvgEye('eye-closed', [
            'M3 3l18 18',
            'M10.6 10.7a3 3 0 0 0 4 4',
            'M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8',
            'M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3'
        ]));

        btn.addEventListener('click', function () {
            const isVisible = input.dataset.passwordVisible === 'true';
            setPasswordVisibility(input, btn, !isVisible);
            renderPasswordEye(btn, !isVisible);
        });

        wrapper.appendChild(btn);
        setPasswordVisibility(input, btn, input.dataset.passwordVisible === 'true');
        renderPasswordEye(btn, input.dataset.passwordVisible === 'true');
        input.dataset.eyeApplied = 'true';
    }

    function initUserModalPasswordEyes() {
        const passwordFields = document.querySelectorAll(
            'input[data-password-field="true"], #addUserModal input[type="password"], #editUserModal input[type="password"], input[name="password"]'
        );
        passwordFields.forEach(addPasswordEye);
    }

    function ensureUserModalOpeners() {
        if (typeof window.openAddUserModal !== 'function') {
            window.openAddUserModal = function () {
                const modal = document.getElementById('addUserModal');
                if (!modal) {
                    console.error('addUserModal not found in DOM');
                    if (typeof window.refreshAdminUi === 'function') {
                        window.refreshAdminUi({
                            tabName: 'add_user',
                            fallbackUrl: '/users/create',
                            options: { historyMode: 'replace' }
                        });
                    }
                    return;
                }

                const messageElement = document.getElementById('message');
                if (messageElement) {
                    messageElement.textContent = '';
                    messageElement.className = '';
                }

                ['fullName', 'username', 'password', 'email_input', 'dateBegin', 'dateEnd'].forEach(function (id) {
                    const el = document.getElementById(id);
                    if (el) el.value = '';
                });

                const roleEl = document.getElementById('userRole');
                if (roleEl) roleEl.value = 'user';

                const orgEl = document.getElementById('userOrganization');
                if (orgEl) orgEl.selectedIndex = 0;

                if (window.AppUi?.setModalVisibility) {
                    window.AppUi.setModalVisibility(modal, true);
                } else if (typeof window.showSiteModal === 'function') {
                    window.showSiteModal(modal);
                }
                setTimeout(initUserModalPasswordEyes, 0);
            };
        }

        if (typeof window.openEditUserModal !== 'function' && typeof window.openEditUserModalFallback === 'function') {
            window.openEditUserModal = window.openEditUserModalFallback;
        }
    }

    function wrapModalOpeners() {
        if (typeof window.openAddUserModal === 'function' && !window.openAddUserModal.__eyeWrapped) {
            const original = window.openAddUserModal;
            const wrapped = function (...args) {
                const result = original.apply(this, args);
                setTimeout(initUserModalPasswordEyes, 0);
                return result;
            };
            wrapped.__eyeWrapped = true;
            window.openAddUserModal = wrapped;
        }

        if (typeof window.openEditUserModal === 'function' && !window.openEditUserModal.__eyeWrapped) {
            const original = window.openEditUserModal;
            const wrapped = function (...args) {
                const result = original.apply(this, args);
                setTimeout(initUserModalPasswordEyes, 0);
                return result;
            };
            wrapped.__eyeWrapped = true;
            window.openEditUserModal = wrapped;
        }
    }

    function bootstrapPasswordTools() {
        ensureUserModalOpeners();
        wrapModalOpeners();
        initUserModalPasswordEyes();
    }

    function startPasswordTools() {
        bootstrapPasswordTools();
        document.addEventListener('admin:user-modal-ready', bootstrapPasswordTools);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startPasswordTools, { once: true });
    } else {
        startPasswordTools();
    }

    window.addPasswordEye = addPasswordEye;
    window.initUserModalPasswordEyes = initUserModalPasswordEyes;
})();
