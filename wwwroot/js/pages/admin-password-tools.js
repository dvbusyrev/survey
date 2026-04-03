(function () {
    function createPasswordEyeSvg(isVisible) {
        if (isVisible) {
            return `
                <svg class="eye-closed" viewBox="0 0 24 24" aria-hidden="true">
                    <path d="M3 3l18 18"></path>
                    <path d="M10.6 10.7a3 3 0 0 0 4 4"></path>
                    <path d="M9.9 5.2A11 11 0 0 1 12 5c6.5 0 10 7 10 7a17.3 17.3 0 0 1-4.1 4.8"></path>
                    <path d="M6.6 6.7A17.7 17.7 0 0 0 2 12s3.5 7 10 7a10.8 10.8 0 0 0 5.2-1.3"></path>
                </svg>
            `;
        }

        return `
            <svg class="eye-open" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12z"></path>
                <circle cx="12" cy="12" r="3"></circle>
            </svg>
        `;
    }

    function initPasswordToggles(root) {
        const scope = root && root.querySelectorAll ? root : document;
        scope.querySelectorAll('.password-toggle-btn').forEach((btn) => {
            if (btn.dataset.eyeInit === 'true') return;

            const targetId = btn.getAttribute('data-target');
            const input = targetId ? document.getElementById(targetId) : null;
            if (!input) return;

            btn.innerHTML = createPasswordEyeSvg(input.type === 'text');
            btn.dataset.eyeInit = 'true';

            btn.addEventListener('click', function () {
                const show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                btn.innerHTML = createPasswordEyeSvg(show);
                btn.setAttribute('aria-label', show ? 'Скрыть пароль' : 'Показать пароль');
                btn.setAttribute('title', show ? 'Скрыть пароль' : 'Показать пароль');
            });
        });
    }

    window.createPasswordEyeSvg = createPasswordEyeSvg;
    window.initPasswordToggles = initPasswordToggles;
})();
