(function () {
    const PAGE_SELECTOR = '[data-page="help-page"]';
    let cleanupHelpPage = null;

    function showHelpMessage(message, type = 'info') {
        window.AppUi.notify(message, type, {
            title: type === 'error' ? 'Ошибка' : 'Успешно'
        });
    }

    function getResponseMessage(responseText, fallbackMessage) {
        if (!responseText) {
            return fallbackMessage;
        }

        try {
            const parsed = JSON.parse(responseText);
            return parsed?.message || parsed?.error || fallbackMessage;
        } catch {
            return responseText.trim() || fallbackMessage;
        }
    }

    async function uploadInstruction(fileInput) {
        const file = fileInput.files?.[0];
        if (!file) {
            return;
        }

        const instructionType = fileInput.dataset.helpType || '';
        const role = fileInput.dataset.helpRole || '';
        const formData = new FormData();
        formData.append('file', file);
        formData.append('type', instructionType);
        formData.append('role', role);

        try {
            const response = await fetch('/help/upload', {
                method: 'POST',
                headers: {
                    RequestVerificationToken: document.getElementById('global-antiforgery-token')?.value || ''
                },
                body: formData
            });
            const responseText = await response.text();

            if (!response.ok) {
                throw new Error(getResponseMessage(responseText, 'Ошибка загрузки файла'));
            }

            const payload = responseText ? JSON.parse(responseText) : {};
            const displayField = document.querySelector(`[data-help-display="${instructionType}"]`);
            if (displayField && payload.displayText) {
                displayField.value = payload.displayText;
                displayField.classList.remove('app-field-placeholder');
            }

            showHelpMessage(payload.message || 'Файл успешно загружен', 'success');
        } catch (error) {
            showHelpMessage(error instanceof Error ? error.message : 'Ошибка загрузки файла', 'error');
        } finally {
            fileInput.value = '';
        }
    }

    function listen(scope, target, type, handler, options) {
        if (!target) {
            return;
        }

        if (scope && typeof scope.listen === 'function') {
            scope.listen(target, type, handler, options);
            return;
        }

        target.addEventListener(type, handler, options);
    }

    function mountHelpPage(page, scope) {
        if (cleanupHelpPage) {
            cleanupHelpPage();
            cleanupHelpPage = null;
        }

        if (!page) {
            return;
        }

        const handleClick = (event) => {
            const trigger = event.target.closest('[data-help-upload-trigger]');
            if (!trigger || !page.contains(trigger)) {
                return;
            }

            const instructionType = trigger.dataset.helpType || '';
            const input = page.querySelector(`[data-help-file-input][data-help-type="${instructionType}"]`);
            input?.click();
        };

        const handleChange = (event) => {
            const input = event.target.closest('[data-help-file-input]');
            if (!input || !page.contains(input)) {
                return;
            }

            uploadInstruction(input);
        };

        listen(scope, page, 'click', handleClick);
        listen(scope, page, 'change', handleChange);

        cleanupHelpPage = () => {
            if (!scope || typeof scope.listen !== 'function') {
                page.removeEventListener('click', handleClick);
                page.removeEventListener('change', handleChange);
            }
        };

        if (scope && typeof scope.add === 'function') {
            scope.add(cleanupHelpPage);
        }
    }

    function initHelpPage(root = document, scope = null) {
        const page = root?.matches?.(PAGE_SELECTOR)
            ? root
            : root?.querySelector?.(PAGE_SELECTOR);
        mountHelpPage(page, scope);
    }

    window.initHelpPage = initHelpPage;
    window.handleSelectChange = function handleSelectChange(select) {
        const role = select?.value;
        const input = document.querySelector(`[data-help-file-input][data-help-role="${role}"]`);
        input?.click();
        if (select) {
            select.value = '';
        }
    };

    if (window.AppPageLifecycle && typeof window.AppPageLifecycle.register === 'function') {
        window.AppPageLifecycle.register(
            'help-page',
            `.app-page${PAGE_SELECTOR}`,
            mountHelpPage
        );
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initHelpPage(document), { once: true });
    } else {
        initHelpPage(document);
    }
})();
