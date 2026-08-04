(() => {
    const rootElement = document.getElementById('root');
    if (!rootElement) {
        return;
    }

    const loginTemplate = document.getElementById('auth-login-template');
    const eyeOpenTemplate = document.getElementById('auth-eye-open-template');
    const eyeClosedTemplate = document.getElementById('auth-eye-closed-template');
    if (!loginTemplate?.content?.firstElementChild) {
        return;
    }

    function parseJsonSafely(value) {
        if (!value) {
            return null;
        }

        try {
            return JSON.parse(value);
        } catch {
            return null;
        }
    }

    function getErrorMessage(responseText, fallbackMessage) {
        const parsed = parseJsonSafely(responseText);
        if (parsed?.message) {
            return parsed.message;
        }

        if (parsed?.error) {
            return parsed.error;
        }

        const normalizedText = typeof responseText === 'string' ? responseText.trim() : '';
        return normalizedText || fallbackMessage;
    }

    rootElement.innerHTML = '';
    const loginContent = loginTemplate.content.firstElementChild.cloneNode(true);
    rootElement.appendChild(loginContent);

    const form = loginContent.querySelector('#loginForm');
    const usernameInput = loginContent.querySelector('#username');
    const passwordInput = loginContent.querySelector('#password');
    const submitButton = loginContent.querySelector('button[type="submit"]');
    const toggleButton = loginContent.querySelector('.password-toggle-btn');

    let isSubmitting = false;
    let isPasswordVisible = false;

    function notifyAuthError(message) {
        const safeMessage = typeof message === 'string' && message.trim().length > 0
            ? message.trim()
            : 'Проверьте правильность введенных данных.';

        window.AppUi.notify(safeMessage, 'error', {
            title: 'Ошибка авторизации',
            duration: 0
        });
    }

    function renderEyeIcon() {
        if (!toggleButton) {
            return;
        }

        toggleButton.innerHTML = '';
        const sourceTemplate = isPasswordVisible ? eyeClosedTemplate : eyeOpenTemplate;
        if (sourceTemplate?.content?.firstElementChild) {
            toggleButton.appendChild(sourceTemplate.content.firstElementChild.cloneNode(true));
        }

        const buttonText = isPasswordVisible ? 'Скрыть пароль' : 'Показать пароль';
        toggleButton.setAttribute('aria-label', buttonText);
        toggleButton.setAttribute('title', buttonText);
    }

    function setSubmittingState(value) {
        isSubmitting = value;
        if (submitButton) {
            submitButton.disabled = value;
        }
    }

    function setPasswordVisibility(value) {
        isPasswordVisible = value;
        if (passwordInput) {
            passwordInput.classList.toggle('is-password-masked', !value);
        }
        renderEyeIcon();
    }

    toggleButton?.addEventListener('mousedown', (event) => event.preventDefault());
    toggleButton?.addEventListener('click', () => {
        setPasswordVisibility(!isPasswordVisible);
        window.requestAnimationFrame(() => {
            if (!passwordInput) {
                return;
            }

            passwordInput.focus({ preventScroll: true });
            const length = passwordInput.value ? passwordInput.value.length : 0;
            if (typeof passwordInput.setSelectionRange === 'function') {
                passwordInput.setSelectionRange(length, length);
            }
        });
    });

    form?.addEventListener('reset', (event) => {
        event.preventDefault();
        setSubmittingState(false);
        if (usernameInput) {
            usernameInput.value = '';
        }
        if (passwordInput) {
            passwordInput.value = '';
        }
        setPasswordVisibility(false);
    });

    form?.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (!usernameInput || !passwordInput || isSubmitting) {
            return;
        }

        setSubmittingState(true);
        try {
            const response = await fetch('/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json;charset=UTF-8'
                },
                body: JSON.stringify([usernameInput.value, passwordInput.value])
            });

            const responseText = await response.text();
            if (!response.ok) {
                throw new Error(getErrorMessage(responseText, 'Проверьте правильность введенных данных.'));
            }

            const payload = parseJsonSafely(responseText);
            if (payload?.role === 'admin') {
                window.location.href = '/survey';
                return;
            }

            if (payload?.role === 'user') {
                window.location.href = '/survey';
                return;
            }

            throw new Error('Неизвестная роль пользователя');
        } catch (error) {
            console.error('Ошибка авторизации:', error);
            notifyAuthError(error instanceof Error ? error.message : 'Произошла ошибка при попытке входа.');
        } finally {
            setSubmittingState(false);
        }
    });

    setPasswordVisibility(false);
})();
