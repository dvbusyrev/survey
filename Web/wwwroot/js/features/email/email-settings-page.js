(function () {
    if (typeof window.initEmailSettingsPage === 'function'
        && typeof window.saveEmailSettings === 'function'
        && typeof window.sendEmailMessage === 'function') {
        window.initEmailSettingsPage();
        return;
    }

    const fieldIds = {
        to: 'email-to',
        subject: 'email-subject',
        content: 'email-content',
        smtpHost: 'email-smtp-host',
        smtpPort: 'email-smtp-port',
        smtpEnableSsl: 'email-smtp-enable-ssl',
        smtpUserName: 'email-smtp-user-name',
        smtpPassword: 'email-smtp-password',
        fromAddress: 'email-from-address',
        fromDisplayName: 'email-from-display-name'
    };

    function getField(id) {
        return document.getElementById(id);
    }

    function getTrimmedValue(id) {
        return (getField(id)?.value || '').trim();
    }

    function splitRecipients(value) {
        return String(value || '')
            .split(/[;,\r\n]+/)
            .map(item => item.trim())
            .filter(Boolean);
    }

    function isValidEmail(email) {
        const value = String(email || '').trim();
        if (!value) {
            return false;
        }

        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    }

    function setInvalidState(id, isInvalid) {
        const element = getField(id);
        if (!element) {
            return;
        }

        element.classList.toggle('invalid', Boolean(isInvalid));
        element.setAttribute('aria-invalid', isInvalid ? 'true' : 'false');
    }

    function clearInvalidStates() {
        Object.values(fieldIds).forEach((id) => setInvalidState(id, false));
    }

    function collectSettings() {
        const smtpPortValue = Number.parseInt(getField(fieldIds.smtpPort)?.value || '', 10);

        return {
            to: getTrimmedValue(fieldIds.to),
            subject: getTrimmedValue(fieldIds.subject),
            content: (getField(fieldIds.content)?.value || '').trim(),
            smtpHost: getTrimmedValue(fieldIds.smtpHost),
            smtpPort: Number.isFinite(smtpPortValue) ? smtpPortValue : 0,
            smtpEnableSsl: (getField(fieldIds.smtpEnableSsl)?.value || 'true') === 'true',
            smtpUserName: getTrimmedValue(fieldIds.smtpUserName),
            smtpPassword: getField(fieldIds.smtpPassword)?.value || '',
            fromAddress: getTrimmedValue(fieldIds.fromAddress),
            fromDisplayName: getTrimmedValue(fieldIds.fromDisplayName)
        };
    }

    function validateSettings(settings) {
        clearInvalidStates();

        const errors = [];
        const recipients = splitRecipients(settings.to);

        if (recipients.length === 0) {
            errors.push('Поле «Кому» должно содержать хотя бы один email.');
            setInvalidState(fieldIds.to, true);
        } else {
            const invalidRecipients = recipients.filter(email => !isValidEmail(email));
            if (invalidRecipients.length > 0) {
                errors.push(`Поле «Кому» содержит некорректные email: ${invalidRecipients.join(', ')}.`);
                setInvalidState(fieldIds.to, true);
            }
        }

        if (!settings.subject) {
            errors.push('Поле «Тема» обязательно.');
            setInvalidState(fieldIds.subject, true);
        }

        if (!settings.content) {
            errors.push('Поле «Содержание» обязательно.');
            setInvalidState(fieldIds.content, true);
        }

        if (!settings.smtpHost) {
            errors.push('Поле «SMTP сервер» обязательно.');
            setInvalidState(fieldIds.smtpHost, true);
        }

        if (!Number.isInteger(settings.smtpPort) || settings.smtpPort < 1 || settings.smtpPort > 65535) {
            errors.push('Поле «Порт SMTP» должно быть числом от 1 до 65535.');
            setInvalidState(fieldIds.smtpPort, true);
        }

        if (!isValidEmail(settings.fromAddress)) {
            errors.push('Поле «Email отправителя» заполнено некорректно.');
            setInvalidState(fieldIds.fromAddress, true);
        }

        const hasUserName = Boolean(settings.smtpUserName);
        const hasPassword = Boolean(settings.smtpPassword);
        if (hasUserName !== hasPassword) {
            errors.push('Логин SMTP и пароль SMTP должны быть заполнены вместе.');
            setInvalidState(fieldIds.smtpUserName, true);
            setInvalidState(fieldIds.smtpPassword, true);
        }

        return errors;
    }

    async function extractApiError(response) {
        const fallbackMessage = typeof window.getResponseErrorMessage === 'function'
            ? window.getResponseErrorMessage(response, 'Ошибка')
            : 'Не удалось выполнить запрос.';

        const responseText = await response.text();
        if (!responseText) {
            return [fallbackMessage];
        }

        try {
            const payload = JSON.parse(responseText);
            if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
                return payload.errors.filter(Boolean);
            }

            if (payload?.error) {
                return [payload.error];
            }

            if (payload?.message) {
                return [payload.message];
            }
        } catch (error) {
            return [responseText];
        }

        return [fallbackMessage];
    }

    function showNotification(message, type, title, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        if (typeof window.siteNotify === 'function') {
            window.siteNotify(normalizedMessage, type, {
                title,
                duration: options.duration ?? (type === 'error' ? 0 : 4500)
            });
            return;
        }

        window.alert(normalizedMessage);
    }

    function showValidationErrors(errors) {
        const normalizedErrors = (Array.isArray(errors) ? errors : [errors])
            .map(item => String(item || '').trim())
            .filter(Boolean);

        if (normalizedErrors.length === 0) {
            return;
        }

        showNotification(normalizedErrors.join(' • '), 'error', 'Проверьте поля', { duration: 0 });
    }

    function setButtonsBusy(isBusy) {
        document
            .querySelectorAll('.email-settings-page__actions button')
            .forEach((button) => {
                button.disabled = isBusy;
            });
    }

    async function postSettings(url, options) {
        const settings = collectSettings();
        const validationErrors = validateSettings(settings);
        if (validationErrors.length > 0) {
            showValidationErrors(validationErrors);
            return false;
        }

        setButtonsBusy(true);

        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(settings)
            });

            if (!response.ok) {
                throw new Error((await extractApiError(response)).join(' '));
            }

            const payload = await response.json();
            clearInvalidStates();
            showNotification(
                payload?.message || options.successMessage,
                'success',
                options.successTitle
            );
            return true;
        } catch (error) {
            showNotification(
                error.message || options.errorMessage || 'Не удалось выполнить операцию.',
                'error',
                options.errorTitle,
                { duration: 0 }
            );
            return false;
        } finally {
            setButtonsBusy(false);
        }
    }

    window.saveEmailSettings = function saveEmailSettings() {
        return postSettings('/mail/settings', {
            successTitle: 'Настройки сохранены',
            successMessage: 'Настройки электронной почты сохранены.',
            errorTitle: 'Сохранение не выполнено',
            errorMessage: 'Не удалось сохранить настройки.'
        });
    };

    window.sendEmailMessage = function sendEmailMessage() {
        return postSettings('/mail/send', {
            successTitle: 'Письмо отправлено',
            successMessage: 'Письмо отправлено.',
            errorTitle: 'Письмо не отправлено',
            errorMessage: 'Не удалось отправить письмо.'
        });
    };

    function bindButton(buttonId, handler) {
        const button = getField(buttonId);
        if (!button || button.dataset.emailActionBound === 'true') {
            return;
        }

        button.dataset.emailActionBound = 'true';
        button.addEventListener('click', (event) => {
            event.preventDefault();
            handler();
        });
    }

    window.initEmailSettingsPage = function initEmailSettingsPage() {
        bindButton('email-save-button', window.saveEmailSettings);
        bindButton('email-send-button', window.sendEmailMessage);
    };

    window.initEmailSettingsPage();
})();
