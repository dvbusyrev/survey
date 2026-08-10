(function () {
    if (window.AppValidation) {
        return;
    }

    let pendingNativeErrors = [];
    let nativeNotificationScheduled = false;

    function resolveElement(target) {
        if (target instanceof Element) {
            return target;
        }

        return typeof target === 'string' ? document.getElementById(target) : null;
    }

    function getFieldHost(element) {
        return element.closest(
            '.app-field-group, .reports-page__field, [data-validation-group]'
        );
    }

    function getVisualTarget(element, options = {}) {
        if (options.visualTarget instanceof Element) {
            return options.visualTarget;
        }

        if (typeof options.visualTarget === 'string') {
            return document.querySelector(options.visualTarget);
        }

        return element;
    }

    function setFieldError(target, message, options = {}) {
        const element = resolveElement(target);
        if (!element) {
            return;
        }

        getVisualTarget(element, options)?.classList.add('invalid');
        element.setAttribute('aria-invalid', 'true');
        if (message) {
            element.dataset.validationMessage = String(message).trim();
        }
    }

    function clearFieldError(target, options = {}) {
        const element = resolveElement(target);
        if (!element) {
            return;
        }

        getVisualTarget(element, options)?.classList.remove('invalid');
        element.classList.remove('invalid');
        element.setAttribute('aria-invalid', 'false');
        delete element.dataset.validationMessage;
    }

    function clearAll(root) {
        const validationRoot = root || document;
        validationRoot.querySelectorAll('.invalid').forEach((element) => element.classList.remove('invalid'));
        validationRoot.querySelectorAll('[aria-invalid="true"]')
            .forEach((element) => {
                element.setAttribute('aria-invalid', 'false');
                delete element.dataset.validationMessage;
            });
    }

    function getFieldLabel(element) {
        const explicitLabel = String(element.dataset.validationLabel || '').trim();
        if (explicitLabel) {
            return explicitLabel;
        }

        const host = getFieldHost(element);
        const label = host?.querySelector(':scope > label, :scope > [data-validation-label]');
        return String(label?.textContent || element.dataset.dateLabel || element.getAttribute('aria-label') || 'Поле')
            .replace(/\s*\*\s*$/, '')
            .trim();
    }

    function getRequiredMessage(element) {
        const explicitMessage = String(element.dataset.requiredMessage || '').trim();
        if (explicitMessage) {
            return explicitMessage;
        }

        const label = getFieldLabel(element);
        if (element.matches('select, [role="group"], [role="listbox"]')) {
            return `Выберите значение в поле «${label}».`;
        }

        return `Заполните поле «${label}».`;
    }

    function isRequiredFieldEmpty(element) {
        if (element.disabled || element.matches('[readonly]')) {
            return false;
        }

        if (element.matches('input[type="checkbox"], input[type="radio"]')) {
            const name = element.getAttribute('name');
            const root = element.form || getFieldHost(element) || document;
            if (name) {
                return !Array.from(root.querySelectorAll(`input[name="${CSS.escape(name)}"]`))
                    .some((input) => input.checked);
            }
            return !element.checked;
        }

        if (element.matches('input, select, textarea')) {
            return !String(element.value || '').trim();
        }

        const valueSelector = element.dataset.validationValueSelector;
        if (valueSelector) {
            return element.querySelectorAll(valueSelector).length === 0;
        }

        const customValue = element.getAttribute('aria-valuenow')
            ?? element.getAttribute('data-value');
        return !String(customValue || '').trim();
    }

    function getRequiredFields(root) {
        const validationRoot = root || document;
        const fields = [];
        if (validationRoot.matches?.('[required], [aria-required="true"]')) {
            fields.push(validationRoot);
        }
        fields.push(...validationRoot.querySelectorAll('[required], [aria-required="true"]'));
        return fields.filter((field, index, items) => items.indexOf(field) === index);
    }

    function validateRequiredFields(root) {
        const invalidFields = [];
        const errors = [];
        getRequiredFields(root).forEach((field) => {
            if (isRequiredFieldEmpty(field)) {
                const message = getRequiredMessage(field);
                setFieldError(field, message);
                invalidFields.push(field);
                errors.push(message);
            } else {
                clearFieldError(field);
            }
        });

        return {
            valid: invalidFields.length === 0,
            invalidFields,
            errors
        };
    }

    function normalizeErrors(errors) {
        return [...new Set((Array.isArray(errors) ? errors : [errors])
            .map((message) => String(message || '').trim())
            .filter(Boolean))];
    }

    function notifyErrors(errors, options = {}) {
        const normalizedErrors = normalizeErrors(errors);
        if (normalizedErrors.length === 0) {
            return;
        }

        window.AppUi?.notify?.(normalizedErrors.join(' • '), 'error', {
            title: options.title || 'Проверьте поля',
            duration: 0
        });
    }

    function focusFirstInvalid(validationResult) {
        const field = validationResult?.invalidFields?.[0];
        if (!field) {
            return;
        }

        if (field.matches('input[data-date-native="true"], input[data-date-enhanced="true"]')) {
            window.AppDate?.focusInput?.(field);
            return;
        }

        field.focus?.({ preventScroll: false });
        field.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' });
    }

    function clearEditedField(event) {
        const field = event.target;
        if (field instanceof Element && field.matches('[aria-invalid="true"], .invalid')) {
            clearFieldError(field);
        }
    }

    function flushNativeErrors() {
        nativeNotificationScheduled = false;
        notifyErrors(pendingNativeErrors);
        pendingNativeErrors = [];
    }

    function handleInvalidField(event) {
        const field = event.target;
        if (!(field instanceof Element)) {
            return;
        }

        event.preventDefault();
        const message = field.validity?.valueMissing
            ? getRequiredMessage(field)
            : `Проверьте значение поля «${getFieldLabel(field)}».`;
        setFieldError(field, message);
        pendingNativeErrors.push(message);
        if (!nativeNotificationScheduled) {
            nativeNotificationScheduled = true;
            queueMicrotask(flushNativeErrors);
        }
    }

    document.addEventListener('input', clearEditedField);
    document.addEventListener('change', clearEditedField);
    document.addEventListener('invalid', handleInvalidField, true);
    document.addEventListener('reset', (event) => {
        window.setTimeout(() => clearAll(event.target), 0);
    }, true);

    window.AppValidation = {
        clearAll,
        clearFieldError,
        focusFirstInvalid,
        getRequiredMessage,
        notifyErrors,
        setFieldError,
        validateRequiredFields
    };
})();
