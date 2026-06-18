(function () {
    function showHelpMessage(message, type = 'info') {
        if (typeof window.siteNotify === 'function') {
            window.siteNotify(message, type, {
                title: type === 'error' ? 'Ошибка' : 'Успешно'
            });
            return;
        }

        window.alert(message);
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
            }

            showHelpMessage(payload.message || 'Файл успешно загружен', 'success');
        } catch (error) {
            showHelpMessage(error instanceof Error ? error.message : 'Ошибка загрузки файла', 'error');
        } finally {
            fileInput.value = '';
        }
    }

    function initHelpPage(root = document) {
        const page = root.querySelector?.('[data-page="help-page"]');
        if (!page || page.dataset.helpPageBound === 'true') {
            return;
        }

        page.dataset.helpPageBound = 'true';
        page.addEventListener('click', (event) => {
            const trigger = event.target.closest('[data-help-upload-trigger]');
            if (!trigger || !page.contains(trigger)) {
                return;
            }

            const instructionType = trigger.dataset.helpType || '';
            const input = page.querySelector(`[data-help-file-input][data-help-type="${instructionType}"]`);
            input?.click();
        });

        page.addEventListener('change', (event) => {
            const input = event.target.closest('[data-help-file-input]');
            if (!input || !page.contains(input)) {
                return;
            }

            uploadInstruction(input);
        });
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

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initHelpPage(document), { once: true });
    } else {
        initHelpPage(document);
    }
})();
