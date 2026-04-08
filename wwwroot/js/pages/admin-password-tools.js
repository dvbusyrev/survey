(function () {
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

    function addPasswordEye(input) {
        if (!input || input.dataset.eyeApplied === 'true') return;
        if (input.closest('.password-eye-wrap')) {
            input.dataset.eyeApplied = 'true';
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'password-eye-wrap';

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'password-eye-btn';
        btn.setAttribute('aria-label', 'Показать пароль');
        btn.innerHTML = `
            <svg class="eye-open" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z"></path>
                <circle cx="12" cy="12" r="3"></circle>
            </svg>
            <svg class="eye-closed" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M3 3l18 18"></path>
                <path d="M10.6 10.7a3 3 0 0 0 4 4"></path>
                <path d="M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8"></path>
                <path d="M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3"></path>
            </svg>
        `;

        btn.addEventListener('click', function () {
            const isVisible = input.dataset.passwordVisible === 'true';
            setPasswordVisibility(input, btn, !isVisible);
        });

        wrapper.appendChild(btn);
        setPasswordVisibility(input, btn, input.dataset.passwordVisible === 'true');
        input.dataset.eyeApplied = 'true';
    }

    function initUserModalPasswordEyes() {
        const passwordFields = document.querySelectorAll(
            '#addUserModal input[data-password-field="true"], #editUserModal input[data-password-field="true"], input[name="password"][data-password-field="true"], #addUserModal input[type="password"], #editUserModal input[type="password"], input[name="password"]'
        );
        passwordFields.forEach(addPasswordEye);
    }

    function ensureUserModalOpeners() {
        if (typeof window.openAddUserModal !== 'function') {
            window.openAddUserModal = function () {
                const modal = document.getElementById('addUserModal');
                if (!modal) {
                    console.error('addUserModal not found in DOM');
                    alert('Форма добавления пользователя не загружена. Сначала откройте список пользователей.');
                    return;
                }

                const messageElement = document.getElementById('message');
                if (messageElement) {
                    messageElement.textContent = '';
                    messageElement.className = '';
                }

                ['fullName', 'username', 'password', 'email_input'].forEach(function (id) {
                    const el = document.getElementById(id);
                    if (el) el.value = '';
                });

                const roleEl = document.getElementById('role_bd');
                if (roleEl) roleEl.value = 'user';

                const orgEl = document.getElementById('organization');
                if (orgEl) orgEl.selectedIndex = 0;

                if (typeof window.showSiteModal === 'function') {
                    window.showSiteModal(modal);
                } else {
                    modal.style.display = 'flex';
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

    document.addEventListener('DOMContentLoaded', function () {
        bootstrapPasswordTools();
        setInterval(function () {
            ensureUserModalOpeners();
            wrapModalOpeners();
        }, 1000);
    });

    window.addPasswordEye = addPasswordEye;
    window.initUserModalPasswordEyes = initUserModalPasswordEyes;
})();
