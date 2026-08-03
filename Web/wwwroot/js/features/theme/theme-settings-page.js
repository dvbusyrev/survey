(function () {
    function getThemeField(id) { return document.getElementById(id); }

    function readThemeValue(id) { return getThemeField(id)?.value || ''; }

    function readThemeTrimmedValue(id) { return readThemeValue(id).trim(); }

    function setThemeValue(id, value) {
        const field = getThemeField(id);
        if (!field) {
            return;
        }

        field.value = value ?? '';
        syncThemeColorSwatch(field);
    }

    function readThemeChecked(id) { return Boolean(getThemeField(id)?.checked); }

    function setThemeChecked(id, isChecked) {
        const field = getThemeField(id);
        if (field) field.checked = Boolean(isChecked);
    }

    function setThemeText(id, value) {
        const field = getThemeField(id);
        if (field) field.textContent = value ?? '';
    }

    const THEME_COLOR_FIELDS = [
        { id: 'theme-font-color', property: 'fontColor', defaultValue: '#343D4B', label: 'Цвет шрифта' },
        { id: 'theme-background-color', property: 'backgroundColor', defaultValue: '#B2A8FF', label: 'Цвет фона' }
    ];

    const THEME_PERCENT_FIELDS = [
        { id: 'theme-header-darken-percent', property: 'headerDarkenPercent', defaultValue: 42 },
        { id: 'theme-footer-darken-percent', property: 'footerDarkenPercent', defaultValue: 42 },
        { id: 'theme-button-darken-percent', property: 'buttonDarkenPercent', defaultValue: 42 },
        { id: 'theme-surface-tint-opacity-percent', property: 'surfaceTintOpacityPercent', defaultValue: 59 }
    ];

    const THEME_EFFECT_FIELDS = [
        { id: 'theme-effect-snow', property: 'effectSnow', label: 'Снег' },
        { id: 'theme-effect-fireworks', property: 'effectFireworks', label: 'Салюты при нажатии' },
        { id: 'theme-effect-grass', property: 'effectGrass', label: 'Трава' },
        { id: 'theme-effect-rain', property: 'effectRain', label: 'Дождь' }
    ];

    const THEME_IMAGE_FIELDS = {
        dataUrl: { id: 'theme-background-image-data-url', property: 'backgroundImageDataUrl' },
        fileName: { id: 'theme-background-image-file-name', property: 'backgroundImageFileName' },
        opacity: { id: 'theme-background-image-opacity', property: 'backgroundImageOpacity', defaultValue: 35, label: 'Прозрачность изображения' }
    };

    const THEME_ALLOWED_IMAGE_PREFIXES = ['data:image/png;base64,', 'data:image/jpeg;base64,', 'data:image/jpg;base64,', 'data:image/webp;base64,'];

    const THEME_INVALID_FIELD_IDS = [
        'theme-background-image-file',
        THEME_IMAGE_FIELDS.opacity.id,
        ...THEME_COLOR_FIELDS.map((field) => field.id),
        ...THEME_PERCENT_FIELDS.map((field) => field.id)
    ];

    function syncThemeColorSwatch(field) {
        if (field?.type !== 'color') {
            return;
        }

        const colorField = field.closest('[data-theme-color-field]');
        if (colorField) {
            colorField.style.backgroundColor = field.value;
            colorField.querySelector('[data-theme-color-swatch]')?.remove();
        }
    }

    function ensureThemeColorFields() {
        THEME_COLOR_FIELDS.forEach((fieldDefinition) => {
            const field = getThemeField(fieldDefinition.id);
            if (!field || field.closest('[data-theme-color-field]')) {
                syncThemeColorSwatch(field);
                return;
            }

            const wrapper = window.AppUi.createElement('div', {
                className: 'app-field theme-settings-page__color-field',
                dataset: { themeColorField: '' }
            });

            field.before(wrapper);
            wrapper.append(field);
            syncThemeColorSwatch(field);
        });
    }

    function readThemeNumberValue(field, { withDefault = false, clamp = false } = {}) {
        const rawValue = readThemeValue(field.id) || (withDefault ? String(field.defaultValue) : '');
        const value = Number.parseInt(rawValue, 10);
        if (!Number.isFinite(value)) return withDefault ? field.defaultValue : 0;
        return clamp ? Math.max(0, Math.min(100, value)) : value;
    }

    function readThemeTextFields(fields, { withDefaults = false } = {}) {
        return Object.fromEntries(fields.map((field) => [
            field.property,
            withDefaults
                ? readThemeTrimmedValue(field.id) || field.defaultValue
                : readThemeTrimmedValue(field.id)
        ]));
    }

    function readThemeCheckboxFields(fields) {
        return Object.fromEntries(fields.map((field) => [field.property, readThemeChecked(field.id)]));
    }

    function readThemePercentFields(fields) {
        return Object.fromEntries(fields.map((field) => [
            field.property,
            readThemeNumberValue(field, { withDefault: true, clamp: true })
        ]));
    }

    function setThemeTextFields(fields, settings, mapValue = (value) => value) {
        fields.forEach((field) => setThemeValue(field.id, mapValue(settings[field.property], field)));
    }

    function setThemeCheckboxFields(fields, settings) {
        fields.forEach((field) => setThemeChecked(field.id, settings[field.property]));
    }

    function getThemeFieldOrCachedValue(id, property) {
        const field = getThemeField(id);
        if (field) {
            return field.value || '';
        }

        return window.__appThemeDraftSettings?.[property]
            || window.__appThemeSavedSettings?.[property]
            || window.__appThemeSettings?.[property]
            || '';
    }

    function readThemeFormSettings({ withDefaults = false } = {}) {
        return {
            backgroundImageDataUrl: getThemeFieldOrCachedValue(
                THEME_IMAGE_FIELDS.dataUrl.id,
                THEME_IMAGE_FIELDS.dataUrl.property
            ),
            backgroundImageFileName: getThemeFieldOrCachedValue(
                THEME_IMAGE_FIELDS.fileName.id,
                THEME_IMAGE_FIELDS.fileName.property
            ),
            backgroundImageOpacity: readThemeNumberValue(THEME_IMAGE_FIELDS.opacity, { withDefault: withDefaults }),
            ...readThemeTextFields(THEME_COLOR_FIELDS, { withDefaults }),
            ...readThemeCheckboxFields(THEME_EFFECT_FIELDS),
            ...readThemePercentFields(THEME_PERCENT_FIELDS)
        };
    }

    function hasThemePayloadValue(rawSettings, camelName, pascalName) {
        return Boolean(rawSettings && typeof rawSettings === 'object')
            && (
                Object.prototype.hasOwnProperty.call(rawSettings, camelName)
                || Object.prototype.hasOwnProperty.call(rawSettings, pascalName)
            );
    }

    function getThemeImageCachedValue(field) {
        return getThemeFieldOrCachedValue(field.id, field.property);
    }

    function normalizeThemeSettingsPayload(rawSettings) {
        const normalizedSource = hasThemePayloadValue(rawSettings, 'backgroundImageDataUrl', 'BackgroundImageDataUrl')
            ? rawSettings
            : {
                ...(rawSettings || {}),
                backgroundImageDataUrl: getThemeImageCachedValue(THEME_IMAGE_FIELDS.dataUrl),
                backgroundImageFileName: getThemeImageCachedValue(THEME_IMAGE_FIELDS.fileName)
            };

        if (typeof window.toCamelThemeSettings === 'function') {
            return window.toCamelThemeSettings(normalizedSource);
        }

        return {
            ...readThemeFormSettings({ withDefaults: true }),
            backgroundImageDataUrl: normalizedSource.backgroundImageDataUrl || normalizedSource.BackgroundImageDataUrl || '',
            backgroundImageFileName: normalizedSource.backgroundImageFileName || normalizedSource.BackgroundImageFileName || ''
        };
    }

    function setThemeInvalidState(id, isInvalid) {
        const element = getThemeField(id);
        if (!element) return;

        const invalidTarget = element.type === 'color'
            ? element.closest('[data-theme-color-field]')
            : element;
        invalidTarget?.classList.toggle('invalid', Boolean(isInvalid));
        element.setAttribute('aria-invalid', isInvalid ? 'true' : 'false');
    }

    function clearThemeInvalidStates() {
        THEME_INVALID_FIELD_IDS.forEach((id) => setThemeInvalidState(id, false));
    }

    const themeSettingsPageState = {
        savedSettings: null,
        isMounted: false,
        effectsDropdownController: null,
        cleanup: null
    };

    function hasThemeSettingsForm() { return THEME_COLOR_FIELDS.every((field) => getThemeField(field.id)); }

    function syncThemeOpacityLabel(settings) {
        setThemeText(`${THEME_IMAGE_FIELDS.opacity.id}-value`, `${settings.backgroundImageOpacity}%`);

        THEME_PERCENT_FIELDS.forEach((field) => {
            setThemeText(`${field.id}-value`, `${settings[field.property]}%`);
        });
    }

    function syncThemeEffectsSummary() {
        const summary = getThemeField('theme-effects-summary');
        if (!summary) {
            return;
        }

        const selectedEffects = [];

        THEME_EFFECT_FIELDS.forEach((field) => {
            const checkbox = getThemeField(field.id);
            const isSelected = Boolean(checkbox?.checked);
            checkbox?.closest('.app-checkbox-option')?.classList.toggle('selected', isSelected);

            if (isSelected) {
                selectedEffects.push(field.label);
            }
        });

        if (selectedEffects.length === 0) {
            summary.replaceChildren(window.AppUi.createElement('span', {
                className: 'theme-settings-page__empty-selection',
                text: 'Эффекты не выбраны'
            }));
            return;
        }

        summary.replaceChildren(window.AppUi.createElement('div', {
            className: 'theme-settings-page__selected-effects-list',
            children: selectedEffects.map((label) => window.AppUi.createElement('span', {
                className: 'app-chip theme-settings-page__selected-effect-item',
                text: label
            }))
        }));
    }

    function syncThemeImageName() {
        const nameField = getThemeField('theme-background-image-name');
        if (!nameField) {
            return;
        }

        const fileName = readThemeValue(THEME_IMAGE_FIELDS.fileName.id);
        nameField.value = fileName
            || getDefaultThemeImageFileName(readThemeValue(THEME_IMAGE_FIELDS.dataUrl.id))
            || 'Изображение не выбрано';
    }

    function getDefaultThemeImageFileName(dataUrl) {
        const normalizedDataUrl = String(dataUrl || '').trim().toLowerCase();
        if (normalizedDataUrl.startsWith('data:image/webp;base64,')) {
            return 'background-image.webp';
        }

        if (normalizedDataUrl.startsWith('data:image/jpeg;base64,') || normalizedDataUrl.startsWith('data:image/jpg;base64,')) {
            return 'background-image.jpg';
        }

        return normalizedDataUrl.startsWith('data:image/png;base64,') ? 'background-image.png' : '';
    }

    function cloneThemeSettings(settings) { return { ...(settings || {}) }; }

    function getSavedThemeSettingsSource() {
        return themeSettingsPageState.savedSettings
            || window.__appThemeSavedSettings
            || window.__appThemeSettings;
    }

    function saveThemeSettingsSnapshot(settings) {
        const snapshot = cloneThemeSettings(settings);
        themeSettingsPageState.savedSettings = snapshot;
        window.__appThemeSavedSettings = cloneThemeSettings(snapshot);
    }

    function applyThemeSettings(settings) {
        if (typeof window.applyThemeSettings === 'function') {
            window.applyThemeSettings(settings);
        }
    }

    function persistThemeSettings(settings) {
        if (typeof window.persistThemeSettings === 'function') {
            window.persistThemeSettings(settings);
        }
    }

    function validateThemeSettingsPayload(settings) {
        clearThemeInvalidStates();

        const errors = [];
        const colorRegex = /^#[0-9a-f]{6}$/i;
        THEME_COLOR_FIELDS.forEach((field) => {
            const value = settings[field.property];
            if (!colorRegex.test(String(value || ''))) {
                errors.push(`Поле «${field.label}» заполнено некорректно`);
                setThemeInvalidState(field.id, true);
            }
        });

        if (!Number.isInteger(settings.backgroundImageOpacity)
            || settings.backgroundImageOpacity < 0
            || settings.backgroundImageOpacity > 100) {
            errors.push(`Поле «${THEME_IMAGE_FIELDS.opacity.label}» должно быть числом от 0 до 100`);
            setThemeInvalidState(THEME_IMAGE_FIELDS.opacity.id, true);
        }

        THEME_PERCENT_FIELDS.forEach((field) => {
            const value = settings[field.property];
            if (!Number.isInteger(value) || value < 0 || value > 100) {
                errors.push('Значения яркости должны быть от 0 до 100');
                setThemeInvalidState(field.id, true);
            }
        });

        const imageValue = String(settings.backgroundImageDataUrl || '');
        if (imageValue) {
            if (!THEME_ALLOWED_IMAGE_PREFIXES.some((prefix) => imageValue.startsWith(prefix))) {
                errors.push('Фоновое изображение должно быть PNG, JPEG или WebP');
                setThemeInvalidState('theme-background-image-file', true);
            }
        }

        return errors;
    }

    async function readThemeImageFile(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(String(reader.result || ''));
            reader.onerror = () => reject(new Error('Не удалось прочитать изображение'));
            reader.readAsDataURL(file);
        });
    }

    async function handleThemeImageChange(event) {
        const input = event?.currentTarget;
        const file = input?.files?.[0];
        if (!file) {
            return;
        }

        try {
            const dataUrl = await readThemeImageFile(file);
            setThemeValue(THEME_IMAGE_FIELDS.dataUrl.id, dataUrl);
            setThemeValue(THEME_IMAGE_FIELDS.fileName.id, file.name);
            syncThemeImageName();
            applyThemeDraftState();
        } catch (error) {
            showThemeToast(error.message || 'Не удалось загрузить изображение', 'error', 'Изображение не загружено', { duration: 0 });
        } finally {
            input.value = '';
        }
    }

    function populateThemeForm(settings) {
        const normalizedSettings = normalizeThemeSettingsPayload(settings);

        setThemeTextFields(THEME_COLOR_FIELDS, normalizedSettings);
        setThemeCheckboxFields(THEME_EFFECT_FIELDS, normalizedSettings);
        setThemeValue(THEME_IMAGE_FIELDS.dataUrl.id, normalizedSettings.backgroundImageDataUrl);
        setThemeValue(
            THEME_IMAGE_FIELDS.fileName.id,
            normalizedSettings.backgroundImageFileName || getDefaultThemeImageFileName(normalizedSettings.backgroundImageDataUrl)
        );
        setThemeValue(THEME_IMAGE_FIELDS.opacity.id, String(normalizedSettings.backgroundImageOpacity));
        setThemeTextFields(THEME_PERCENT_FIELDS, normalizedSettings, (value) => String(value));

        syncThemeOpacityLabel(normalizedSettings);
        syncThemeEffectsSummary();
        syncThemeImageName();
    }

    function applyThemeDraftState() {
        if (!hasThemeSettingsForm()) {
            return normalizeThemeSettingsPayload(
                window.__appThemeDraftSettings
                || window.__appThemeSavedSettings
                || window.__appThemeSettings
            );
        }

        const settings = normalizeThemeSettingsPayload(readThemeFormSettings());
        syncThemeOpacityLabel(settings);
        window.__appThemeDraftSettings = cloneThemeSettings(settings);
        applyThemeSettings(settings);

        return settings;
    }

    function resetThemeSettings() {
        const savedSettings = normalizeThemeSettingsPayload(getSavedThemeSettingsSource());

        clearThemeInvalidStates();
        window.__appThemeDraftSettings = null;
        populateThemeForm(savedSettings);
        saveThemeSettingsSnapshot(savedSettings);
        applyThemeSettings(savedSettings);
    }

    async function submitThemeSettings() {
        const settings = normalizeThemeSettingsPayload(readThemeFormSettings());
        const validationErrors = validateThemeSettingsPayload(settings);
        if (validationErrors.length > 0) {
            showThemeValidationErrors(validationErrors);
            return false;
        }

        setThemeButtonsBusy(true);

        try {
            const response = await fetch('/theme/settings', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(settings)
            });

            if (!response.ok) {
                throw new Error((await extractThemeApiErrors(response)).join(' '));
            }

            const payload = await response.json();
            clearThemeInvalidStates();
            saveThemeSettingsSnapshot(settings);
            window.__appThemeDraftSettings = null;
            persistThemeSettings(settings);
            applyThemeSettings(settings);
            showThemeToast(
                payload?.message || 'Настройки темы сохранены',
                'success',
                'Настройки сохранены'
            );
            return true;
        } catch (error) {
            showThemeToast(
                error.message || 'Не удалось сохранить настройки темы',
                'error',
                'Сохранение не выполнено',
                { duration: 0 }
            );
            return false;
        } finally {
            setThemeButtonsBusy(false);
        }
    }

    async function extractThemeApiErrors(response) {
        const fallbackMessage = typeof window.getResponseErrorMessage === 'function'
            ? window.getResponseErrorMessage(response, 'Ошибка')
            : 'Не удалось выполнить запрос';
        const responseText = await response.text();
        if (!responseText) {
            return [fallbackMessage];
        }

        try {
            const payload = JSON.parse(responseText);
            if (Array.isArray(payload?.errors) && payload.errors.length > 0) {
                return payload.errors.filter(Boolean);
            }

            return [payload?.error || payload?.message || fallbackMessage];
        } catch (error) {
            return [responseText];
        }
    }

    function showThemeToast(message, type, title, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        window.AppUi.notify(normalizedMessage, type, {
            title,
            duration: options.duration ?? (type === 'error' ? 0 : 4500)
        });
    }

    function showThemeValidationErrors(errors) {
        const normalizedErrors = (Array.isArray(errors) ? errors : [errors])
            .map((item) => String(item || '').trim())
            .filter(Boolean);

        if (normalizedErrors.length > 0) {
            showThemeToast(normalizedErrors.join(' • '), 'error', 'Проверьте поля', { duration: 0 });
        }
    }

    function setThemeButtonsBusy(isBusy) {
        document
            .querySelectorAll('.theme-settings-page__actions button')
            .forEach((button) => {
                button.disabled = isBusy;
            });
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

    function bindThemeAction(buttonId, action, scope) {
        const button = getThemeField(buttonId);
        if (!button) {
            return;
        }

        listen(scope, button, 'click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            action();
        });
    }

    function bindThemeInput(id, eventName = 'input', scope) {
        const element = getThemeField(id);
        if (!element) {
            return;
        }

        listen(scope, element, eventName, () => {
            syncThemeColorSwatch(element);
            applyThemeDraftState();
        });
    }

    function bindThemeEffectInput(id, scope) {
        const element = getThemeField(id);
        if (!element) {
            return;
        }

        listen(scope, element, 'change', () => {
            syncThemeEffectsSummary();
            applyThemeDraftState();
        });
    }

    function bindThemeInputs(fields, scope, { eventName = 'input', colorPicker = false } = {}) {
        fields.forEach((field) => {
            bindThemeInput(field.id, eventName, scope);
            if (colorPicker) {
                bindThemeColorPickerCursor(field.id, scope);
            }
        });
    }

    function bindThemeEffectsPicker(scope) {
        const trigger = getThemeField('theme-effects-toggle');
        const dropdown = getThemeField('theme-effects-dropdown');
        const root = trigger?.closest('.theme-settings-page__effects-picker');
        if (!root || !trigger || !dropdown || typeof window.AppUi?.createMultiselect !== 'function') {
            return;
        }

        const multiselect = window.AppUi.createMultiselect({
            root,
            trigger,
            menu: dropdown,
            openClass: 'is-open',
            hiddenClass: 'is-hidden'
        });
        themeSettingsPageState.effectsDropdownController = multiselect.controller;
    }

    function bindThemeColorPickerCursor(id, scope) {
        const element = getThemeField(id);
        if (!element) {
            return;
        }

        const suppressCursor = () => {
            window.AppPickerCursor?.suppress(element);
        };

        listen(scope, element, 'pointerdown', suppressCursor);
        listen(scope, element, 'click', suppressCursor);
        listen(scope, element, 'keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                suppressCursor();
            }
        });
    }

    function mountThemeSettingsPage(pageRoot, scope) {
        if (themeSettingsPageState.cleanup) {
            themeSettingsPageState.cleanup();
            themeSettingsPageState.cleanup = null;
        }

        if (!hasThemeSettingsForm()) {
            themeSettingsPageState.isMounted = false;
            return;
        }

        ensureThemeColorFields();
        themeSettingsPageState.isMounted = true;
        bindThemeAction('theme-save-button', submitThemeSettings, scope);
        bindThemeAction('theme-reset-button', resetThemeSettings, scope);
        bindThemeAction('theme-background-image-upload', () => getThemeField('theme-background-image-file')?.click(), scope);
        bindThemeInputs(THEME_COLOR_FIELDS, scope, { colorPicker: true });
        bindThemeInput(THEME_IMAGE_FIELDS.opacity.id, 'input', scope);
        bindThemeInputs(THEME_PERCENT_FIELDS, scope);
        THEME_EFFECT_FIELDS.forEach((field) => bindThemeEffectInput(field.id, scope));
        bindThemeEffectsPicker(scope);

        const fileInput = getThemeField('theme-background-image-file');
        if (fileInput) {
            listen(scope, fileInput, 'change', handleThemeImageChange);
        }

        const savedSettings = normalizeThemeSettingsPayload(
            window.__appThemeSavedSettings
            || window.__appThemeSettings
            || readThemeFormSettings()
        );
        const initialSettings = normalizeThemeSettingsPayload(
            window.__appThemeDraftSettings
            || savedSettings
        );
        populateThemeForm(initialSettings);
        saveThemeSettingsSnapshot(savedSettings);
        if (window.__appThemeDraftSettings) {
            applyThemeSettings(initialSettings);
        }

        const cleanup = () => {
            if (!themeSettingsPageState.isMounted) {
                return;
            }

            themeSettingsPageState.isMounted = false;
            themeSettingsPageState.effectsDropdownController?.destroy?.();
            themeSettingsPageState.effectsDropdownController = null;
            const nextSettings = normalizeThemeSettingsPayload(getSavedThemeSettingsSource());
            window.__appThemeDraftSettings = null;
            applyThemeSettings(nextSettings);
        };

        themeSettingsPageState.cleanup = cleanup;
        if (scope && typeof scope.add === 'function') {
            scope.add(cleanup);
        }
    }

    function initThemeSettingsPage(root = document, scope = null) {
        const pageRoot = root?.matches?.('[data-page="theme-settings-page"]')
            ? root
            : root?.querySelector?.('[data-page="theme-settings-page"]');
        if (!pageRoot) {
            return;
        }

        mountThemeSettingsPage(pageRoot, scope);
    }

    if (window.AppPageLifecycle && typeof window.AppPageLifecycle.register === 'function') {
        window.AppPageLifecycle.register(
            'theme-settings-page',
            '.app-page[data-page="theme-settings-page"]',
            mountThemeSettingsPage
        );
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initThemeSettingsPage(document), { once: true });
    } else {
        initThemeSettingsPage(document);
    }
})();
