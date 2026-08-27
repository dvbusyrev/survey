(function () {
    if (typeof window.initEmailSettingsPage === 'function'
        && typeof window.saveEmailSettings === 'function'
        && typeof window.sendEmailMessage === 'function') {
        window.initEmailSettingsPage();
        return;
    }

    function getEmailField(id) {
        return document.getElementById(id);
    }

    function getEmailTrimmedValue(id) {
        return (getEmailField(id)?.value || '').trim();
    }

    function splitEmailRecipients(value) {
        return String(value || '')
            .split(/[;,\r\n]+/)
            .map((item) => item.trim())
            .filter(Boolean);
    }

    function isValidEmailAddress(email) {
        const value = String(email || '').trim();
        if (!value) {
            return false;
        }

        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    }

    function setEmailInvalidState(id, errorMessage) {
        const element = getEmailField(id);
        if (!element) {
            return;
        }

        if (errorMessage) {
            window.AppValidation?.setFieldError?.(element, errorMessage);
        } else {
            window.AppValidation?.clearFieldError?.(element);
        }
    }

    function clearEmailInvalidStates() {
        [
            'email-to',
            'email-subject',
            'email-content',
            'email-smtp-host',
            'email-smtp-port',
            'email-smtp-user-name',
            'email-smtp-password',
            'email-from-address',
            'email-from-display-name'
        ].forEach((id) => setEmailInvalidState(id, false));
    }

    const emailSettingsPageState = {
        savedSettings: null
    };

    function initializeEmailPasswordToggle() {
        const input = getEmailField('email-smtp-password');
        window.AppPassword?.mountField(input);
    }

    function isMessagePage() {
        return document.querySelector('[data-page="mail-compose"]') !== null;
    }

    function collectEmailMessagePayload() {
        return {
            to: getEmailTrimmedValue('email-to'),
            subject: getEmailTrimmedValue('email-subject'),
            content: (getEmailField('email-content')?.value || '').trim()
        };
    }

    function collectEmailSenderPayload() {
        const smtpPortValue = Number.parseInt(getEmailField('email-smtp-port')?.value || '', 10);

        return {
            smtpHost: getEmailTrimmedValue('email-smtp-host'),
            smtpPort: Number.isFinite(smtpPortValue) ? smtpPortValue : 0,
            smtpEnableSsl: (getEmailField('email-smtp-enable-ssl')?.value || 'true') === 'true',
            smtpUserName: getEmailTrimmedValue('email-smtp-user-name'),
            smtpPassword: getEmailField('email-smtp-password')?.value || '',
            fromAddress: getEmailTrimmedValue('email-from-address'),
            fromDisplayName: getEmailTrimmedValue('email-from-display-name')
        };
    }

    function collectCurrentPagePayload() {
        return isMessagePage()
            ? collectEmailMessagePayload()
            : collectEmailSenderPayload();
    }

    function setEmailFieldValue(id, value) {
        const element = getEmailField(id);
        if (!element) {
            return;
        }

        element.value = value == null ? '' : String(value);
    }

    function populateEmailSettingsForm(settings) {
        const normalizedSettings = settings || {};
        if (isMessagePage()) {
            setEmailFieldValue('email-to', normalizedSettings.to);
            setEmailFieldValue('email-subject', normalizedSettings.subject);
            setEmailFieldValue('email-content', normalizedSettings.content);
            return;
        }

        setEmailFieldValue('email-smtp-host', normalizedSettings.smtpHost);
        setEmailFieldValue('email-smtp-port', normalizedSettings.smtpPort || '');
        setEmailFieldValue('email-smtp-enable-ssl', normalizedSettings.smtpEnableSsl ? 'true' : 'false');
        setEmailFieldValue('email-smtp-user-name', normalizedSettings.smtpUserName);
        setEmailFieldValue('email-smtp-password', '');
        setEmailFieldValue('email-from-address', normalizedSettings.fromAddress);
        setEmailFieldValue('email-from-display-name', normalizedSettings.fromDisplayName);
    }

    function resetEmailSettings() {
        clearEmailInvalidStates();
        populateEmailSettingsForm(emailSettingsPageState.savedSettings || collectCurrentPagePayload());
    }

    function validateEmailMessagePayload(settings) {
        clearEmailInvalidStates();

        const errors = [];
        const recipients = splitEmailRecipients(settings.to);

        if (recipients.length === 0) {
            const message = 'Укажите хотя бы одну эл. почту получателя.';
            errors.push(message);
            setEmailInvalidState('email-to', message);
        } else {
            const invalidRecipients = recipients.filter((email) => !isValidEmailAddress(email));
            if (invalidRecipients.length > 0) {
                const message = `Проверьте эл. почту получателя: ${invalidRecipients.join(', ')}.`;
                errors.push(message);
                setEmailInvalidState('email-to', message);
            }
        }

        if (!settings.subject) {
            const message = 'Введите тему письма.';
            errors.push(message);
            setEmailInvalidState('email-subject', message);
        }

        if (!settings.content) {
            const message = 'Введите текст письма.';
            errors.push(message);
            setEmailInvalidState('email-content', message);
        }

        return errors;
    }

    function validateEmailSenderPayload(settings) {
        clearEmailInvalidStates();

        const errors = [];
        if (!settings.smtpHost) {
            const message = 'Введите SMTP сервер.';
            errors.push(message);
            setEmailInvalidState('email-smtp-host', message);
        }

        if (!settings.smtpUserName) {
            const message = 'Введите логин SMTP.';
            errors.push(message);
            setEmailInvalidState('email-smtp-user-name', message);
        }

        if (!Number.isInteger(settings.smtpPort) || settings.smtpPort < 1 || settings.smtpPort > 65535) {
            const message = 'Порт SMTP должен быть числом от 1 до 65535.';
            errors.push(message);
            setEmailInvalidState('email-smtp-port', message);
        }

        if (!settings.fromAddress) {
            const message = 'Введите эл. почту отправителя.';
            errors.push(message);
            setEmailInvalidState('email-from-address', message);
        } else if (!isValidEmailAddress(settings.fromAddress)) {
            const message = 'Проверьте эл. почту отправителя.';
            errors.push(message);
            setEmailInvalidState('email-from-address', message);
        }

        if (!settings.fromDisplayName) {
            const message = 'Введите имя отправителя.';
            errors.push(message);
            setEmailInvalidState('email-from-display-name', message);
        }

        return errors;
    }

    async function extractEmailApiErrors(response) {
        const fallbackMessage = typeof window.getResponseErrorMessage === 'function'
            ? window.getResponseErrorMessage(response, 'Не удалось выполнить операцию с письмом')
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

    function showEmailToast(message, type, title, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        window.AppUi.notify(normalizedMessage, type, {
            title,
            duration: options.duration ?? (type === 'error' ? 0 : 4500)
        });
    }

    function showEmailValidationErrors(errors) {
        const normalizedErrors = (Array.isArray(errors) ? errors : [errors])
            .map((item) => String(item || '').trim())
            .filter(Boolean);

        if (normalizedErrors.length === 0) {
            return;
        }

        showEmailToast(normalizedErrors.join(' • '), 'error', 'Проверьте поля', { duration: 0 });
    }

    function setEmailButtonsBusy(isBusy, options = {}) {
        const activeButtonId = options.activeButtonId || '';
        const busyLabel = options.busyLabel || '';

        document
            .querySelectorAll('.email-settings-page__actions button')
            .forEach((button) => {
                button.disabled = isBusy;
                if (!button.dataset.defaultLabel) {
                    button.dataset.defaultLabel = button.textContent || '';
                }

                if (isBusy) {
                    button.textContent = activeButtonId && button.id === activeButtonId
                        ? busyLabel || button.dataset.defaultLabel || button.textContent
                        : button.dataset.defaultLabel || button.textContent;
                    return;
                }

                button.textContent = button.dataset.defaultLabel || button.textContent;
            });
    }

    async function submitEmailSettings(url, options) {
        const settings = options.payloadType === 'message'
            ? collectEmailMessagePayload()
            : collectEmailSenderPayload();
        const validationErrors = options.payloadType === 'message'
            ? validateEmailMessagePayload(settings)
            : validateEmailSenderPayload(settings);
        if (validationErrors.length > 0) {
            showEmailValidationErrors(validationErrors);
            return false;
        }

        setEmailButtonsBusy(true, {
            activeButtonId: options.busyButtonId,
            busyLabel: options.busyLabel
        });

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
                throw new Error((await extractEmailApiErrors(response)).join(' '));
            }

            const payload = await response.json();
            clearEmailInvalidStates();
            if (options.updateSavedSettings) {
                emailSettingsPageState.savedSettings = options.payloadType === 'sender'
                    ? { ...settings, smtpPassword: '' }
                    : { ...settings };
            }
            if (options.payloadType === 'sender') {
                setEmailFieldValue('email-smtp-password', '');
            }
            showEmailToast(
                payload?.message || options.successMessage,
                'success',
                options.successTitle
            );
            return true;
        } catch (error) {
            showEmailToast(
                error.message || options.errorMessage || 'Не удалось выполнить операцию.',
                'error',
                options.errorTitle,
                { duration: 0 }
            );
            return false;
        } finally {
            setEmailButtonsBusy(false);
        }
    }

    window.saveEmailSettings = function saveEmailSettings() {
        const messagePage = isMessagePage();
        return submitEmailSettings(messagePage ? '/email/message' : '/email/settings', {
            busyButtonId: 'email-save-button',
            busyLabel: 'Сохранение...',
            successTitle: messagePage ? 'Письмо сохранено' : 'Настройки сохранены',
            successMessage: messagePage ? 'Письмо сохранено.' : 'Настройки отправителя сохранены.',
            errorTitle: 'Сохранение не выполнено',
            errorMessage: messagePage ? 'Не удалось сохранить письмо.' : 'Не удалось сохранить настройки отправителя.',
            payloadType: messagePage ? 'message' : 'sender',
            updateSavedSettings: true
        });
    };

    window.sendEmailMessage = function sendEmailMessage() {
        return submitEmailSettings('/email/send', {
            busyButtonId: 'email-send-button',
            busyLabel: 'Отправка...',
            successTitle: 'Письмо отправлено',
            successMessage: 'Письмо отправлено.',
            errorTitle: 'Письмо не отправлено',
            errorMessage: 'Не удалось отправить письмо.',
            payloadType: 'message'
        });
    };

    function bindEmailAction(buttonId, action) {
        const button = document.getElementById(buttonId);
        if (!button || button.dataset.emailActionBound === 'true') {
            return;
        }

        button.dataset.emailActionBound = 'true';
        button.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            action();
        });
    }

    window.initEmailSettingsPage = function initEmailSettingsPage() {
        emailSettingsPageState.savedSettings = collectCurrentPagePayload();
        initializeEmailPasswordToggle();
        bindEmailAction('email-reset-button', resetEmailSettings);
        bindEmailAction('email-save-button', window.saveEmailSettings);
        bindEmailAction('email-send-button', window.sendEmailMessage);
    };

    if (window.AppPageLifecycle?.register) {
        window.AppPageLifecycle.register(
            'email-settings-page',
            '[data-page="mail-compose"], [data-page="mail-settings-page"]',
            () => {
                window.initEmailSettingsPage();
            }
        );
    } else {
        window.initEmailSettingsPage();
    }
})();
