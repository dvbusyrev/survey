(function () {
    function getThemeField(id) {
        return document.getElementById(id);
    }

    function getThemeTrimmedValue(id) {
        return (getThemeField(id)?.value || '').trim();
    }

    const THEME_PERCENT_FIELDS = [
        ['theme-header-darken-percent', 'headerDarkenPercent', 42],
        ['theme-footer-darken-percent', 'footerDarkenPercent', 42],
        ['theme-button-darken-percent', 'buttonDarkenPercent', 42],
        ['theme-surface-tint-opacity-percent', 'surfaceTintOpacityPercent', 59]
    ];

    const THEME_EFFECT_FIELDS = [
        ['theme-effect-snow', 'Снег'],
        ['theme-effect-fireworks', 'Салюты при нажатии'],
        ['theme-effect-grass', 'Трава'],
        ['theme-effect-rain', 'Дождь']
    ];

    function getThemePercentValue(id, fallback) {
        const value = Number.parseInt(getThemeField(id)?.value || '', 10);
        if (!Number.isFinite(value)) {
            return fallback;
        }

        return Math.max(0, Math.min(100, value));
    }

    function getThemeImagePayloadValue() {
        const imageField = getThemeField('theme-background-image-data-url');
        if (imageField) {
            return imageField.value || '';
        }

        return window.__appThemeDraftSettings?.backgroundImageDataUrl
            || window.__appThemeSavedSettings?.backgroundImageDataUrl
            || window.__appThemeSettings?.backgroundImageDataUrl
            || '';
    }

    function getThemeImageFileNamePayloadValue() {
        const fileNameField = getThemeField('theme-background-image-file-name');
        if (fileNameField) {
            return fileNameField.value || '';
        }

        return window.__appThemeDraftSettings?.backgroundImageFileName
            || window.__appThemeSavedSettings?.backgroundImageFileName
            || window.__appThemeSettings?.backgroundImageFileName
            || '';
    }

    function hasThemePayloadValue(rawSettings, camelName, pascalName) {
        return Boolean(rawSettings && typeof rawSettings === 'object')
            && (
                Object.prototype.hasOwnProperty.call(rawSettings, camelName)
                || Object.prototype.hasOwnProperty.call(rawSettings, pascalName)
            );
    }

    function normalizeThemeSettingsPayload(rawSettings) {
        const normalizedSource = hasThemePayloadValue(rawSettings, 'backgroundImageDataUrl', 'BackgroundImageDataUrl')
            ? rawSettings
            : {
                ...(rawSettings || {}),
                backgroundImageDataUrl: getThemeImagePayloadValue(),
                backgroundImageFileName: getThemeImageFileNamePayloadValue()
            };

        if (typeof window.toCamelThemeSettings === 'function') {
            return window.toCamelThemeSettings(normalizedSource);
        }

        return {
            fontColor: getThemeTrimmedValue('theme-font-color') || '#343D4B',
            backgroundColor: getThemeTrimmedValue('theme-background-color') || '#B2A8FF',
            effectSnow: Boolean(getThemeField('theme-effect-snow')?.checked),
            effectFireworks: Boolean(getThemeField('theme-effect-fireworks')?.checked),
            effectGrass: Boolean(getThemeField('theme-effect-grass')?.checked),
            effectRain: Boolean(getThemeField('theme-effect-rain')?.checked),
            backgroundImageDataUrl: normalizedSource.backgroundImageDataUrl || normalizedSource.BackgroundImageDataUrl || '',
            backgroundImageFileName: normalizedSource.backgroundImageFileName || normalizedSource.BackgroundImageFileName || '',
            backgroundImageOpacity: Number.parseInt(getThemeField('theme-background-image-opacity')?.value || '35', 10) || 35,
            headerDarkenPercent: getThemePercentValue('theme-header-darken-percent', 42),
            footerDarkenPercent: getThemePercentValue('theme-footer-darken-percent', 42),
            buttonDarkenPercent: getThemePercentValue('theme-button-darken-percent', 42),
            surfaceTintOpacityPercent: getThemePercentValue('theme-surface-tint-opacity-percent', 59)
        };
    }

    function setThemeInvalidState(id, isInvalid) {
        const element = getThemeField(id);
        if (!element) {
            return;
        }

        element.classList.toggle('invalid', Boolean(isInvalid));
        element.setAttribute('aria-invalid', isInvalid ? 'true' : 'false');
    }

    function clearThemeInvalidStates() {
        [
            'theme-font-color',
            'theme-background-color',
            'theme-background-image-file',
            'theme-background-image-opacity',
            ...THEME_PERCENT_FIELDS.map(([id]) => id)
        ].forEach((id) => setThemeInvalidState(id, false));
    }

    const themeSettingsPageState = {
        savedSettings: null,
        isMounted: false,
        cleanup: null
    };

    function hasThemeSettingsForm() {
        return Boolean(
            getThemeField('theme-font-color')
            && getThemeField('theme-background-color')
        );
    }

    function syncThemeOpacityLabel(settings) {
        const opacityValue = document.getElementById('theme-background-image-opacity-value');
        if (opacityValue) {
            opacityValue.textContent = `${settings.backgroundImageOpacity}%`;
        }

        THEME_PERCENT_FIELDS.forEach(([fieldId, propertyName]) => {
            const valueNode = document.getElementById(`${fieldId}-value`);
            if (valueNode) {
                valueNode.textContent = `${settings[propertyName]}%`;
            }
        });
    }

    function syncThemeEffectsSummary() {
        const summary = document.getElementById('theme-effects-summary');
        if (!summary) {
            return;
        }

        summary.replaceChildren();
        const selectedEffects = [];

        THEME_EFFECT_FIELDS.forEach(([fieldId, label]) => {
            const checkbox = getThemeField(fieldId);
            const isSelected = Boolean(checkbox?.checked);
            checkbox?.closest('.app-checkbox-option')?.classList.toggle('selected', isSelected);

            if (isSelected) {
                selectedEffects.push(label);
            }
        });

        if (selectedEffects.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'theme-settings-page__empty-selection';
            empty.textContent = 'Эффекты не выбраны';
            summary.appendChild(empty);
            return;
        }

        const list = document.createElement('div');
        list.className = 'theme-settings-page__selected-effects-list';
        selectedEffects.forEach((label) => {
            const item = document.createElement('div');
            item.className = 'theme-settings-page__selected-effect-item';
            item.appendChild(document.createTextNode(label));
            list.appendChild(item);
        });
        summary.appendChild(list);
    }

    function setThemeEffectsDropdownOpen(isOpen) {
        const trigger = getThemeField('theme-effects-toggle');
        const dropdown = document.getElementById('theme-effects-dropdown');
        if (!trigger || !dropdown) {
            return;
        }

        dropdown.classList.toggle('is-hidden', !isOpen);
        trigger.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    }

    function syncThemeImageName() {
        const nameField = getThemeField('theme-background-image-name');
        const dataField = getThemeField('theme-background-image-data-url');
        const fileNameField = getThemeField('theme-background-image-file-name');
        if (!nameField) {
            return;
        }

        const fileName = fileNameField?.value || '';
        nameField.value = fileName || getDefaultThemeImageFileName(dataField?.value) || 'Изображение не выбрано';
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

    function collectThemeSettingsPayload() {
        const opacityValue = Number.parseInt(getThemeField('theme-background-image-opacity')?.value || '', 10);

        return {
            fontColor: getThemeTrimmedValue('theme-font-color'),
            backgroundColor: getThemeTrimmedValue('theme-background-color'),
            effectSnow: Boolean(getThemeField('theme-effect-snow')?.checked),
            effectFireworks: Boolean(getThemeField('theme-effect-fireworks')?.checked),
            effectGrass: Boolean(getThemeField('theme-effect-grass')?.checked),
            effectRain: Boolean(getThemeField('theme-effect-rain')?.checked),
            backgroundImageDataUrl: getThemeImagePayloadValue(),
            backgroundImageFileName: getThemeImageFileNamePayloadValue(),
            backgroundImageOpacity: Number.isFinite(opacityValue) ? opacityValue : 0,
            headerDarkenPercent: getThemePercentValue('theme-header-darken-percent', 42),
            footerDarkenPercent: getThemePercentValue('theme-footer-darken-percent', 42),
            buttonDarkenPercent: getThemePercentValue('theme-button-darken-percent', 42),
            surfaceTintOpacityPercent: getThemePercentValue('theme-surface-tint-opacity-percent', 59)
        };
    }

    function validateThemeSettingsPayload(settings) {
        clearThemeInvalidStates();

        const errors = [];
        const colorRegex = /^#[0-9a-f]{6}$/i;
        const colorFields = [
            ['theme-font-color', settings.fontColor, 'Цвет шрифта'],
            ['theme-background-color', settings.backgroundColor, 'Цвет фона']
        ];

        colorFields.forEach(([fieldId, value, label]) => {
            if (!colorRegex.test(String(value || ''))) {
                errors.push(`Поле «${label}» заполнено некорректно`);
                setThemeInvalidState(fieldId, true);
            }
        });

        if (!Number.isInteger(settings.backgroundImageOpacity)
            || settings.backgroundImageOpacity < 0
            || settings.backgroundImageOpacity > 100) {
            errors.push('Поле «Прозрачность изображения» должно быть числом от 0 до 100.');
            setThemeInvalidState('theme-background-image-opacity', true);
        }

        THEME_PERCENT_FIELDS.forEach(([fieldId, propertyName]) => {
            const value = settings[propertyName];
            if (!Number.isInteger(value) || value < 0 || value > 100) {
                errors.push('Значения яркости должны быть от 0 до 100.');
                setThemeInvalidState(fieldId, true);
            }
        });

        const imageValue = String(settings.backgroundImageDataUrl || '');
        if (imageValue) {
            const allowedPrefixes = [
                'data:image/png;base64,',
                'data:image/jpeg;base64,',
                'data:image/jpg;base64,',
                'data:image/webp;base64,'
            ];

            if (!allowedPrefixes.some((prefix) => imageValue.startsWith(prefix))) {
                errors.push('Фоновое изображение должно быть PNG, JPEG или WebP.');
                setThemeInvalidState('theme-background-image-file', true);
            }
        }

        return errors;
    }

    async function readThemeImageFile(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(String(reader.result || ''));
            reader.onerror = () => reject(new Error('Не удалось прочитать изображение.'));
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
            const hiddenField = getThemeField('theme-background-image-data-url');
            if (hiddenField) {
                hiddenField.value = dataUrl;
            }
            const fileNameField = getThemeField('theme-background-image-file-name');
            if (fileNameField) {
                fileNameField.value = file.name;
            }
            syncThemeImageName();
            applyThemeDraftState();
        } catch (error) {
            showEmailToast(error.message || 'Не удалось загрузить изображение', 'error', 'Изображение не загружено', { duration: 0 });
        } finally {
            input.value = '';
        }
    }

    function populateThemeForm(settings) {
        const normalizedSettings = normalizeThemeSettingsPayload(settings);

        const fontColor = getThemeField('theme-font-color');
        if (fontColor) {
            fontColor.value = normalizedSettings.fontColor;
        }

        const backgroundColor = getThemeField('theme-background-color');
        if (backgroundColor) {
            backgroundColor.value = normalizedSettings.backgroundColor;
        }

        const effectSnow = getThemeField('theme-effect-snow');
        if (effectSnow) {
            effectSnow.checked = normalizedSettings.effectSnow;
        }

        const effectFireworks = getThemeField('theme-effect-fireworks');
        if (effectFireworks) {
            effectFireworks.checked = normalizedSettings.effectFireworks;
        }

        const effectGrass = getThemeField('theme-effect-grass');
        if (effectGrass) {
            effectGrass.checked = normalizedSettings.effectGrass;
        }

        const effectRain = getThemeField('theme-effect-rain');
        if (effectRain) {
            effectRain.checked = normalizedSettings.effectRain;
        }

        const imageDataField = getThemeField('theme-background-image-data-url');
        if (imageDataField) {
            imageDataField.value = normalizedSettings.backgroundImageDataUrl;
        }

        const imageFileNameField = getThemeField('theme-background-image-file-name');
        if (imageFileNameField) {
            imageFileNameField.value = normalizedSettings.backgroundImageFileName || getDefaultThemeImageFileName(normalizedSettings.backgroundImageDataUrl);
        }

        const opacityField = getThemeField('theme-background-image-opacity');
        if (opacityField) {
            opacityField.value = String(normalizedSettings.backgroundImageOpacity);
        }

        THEME_PERCENT_FIELDS.forEach(([fieldId, propertyName]) => {
            const field = getThemeField(fieldId);
            if (field) {
                field.value = String(normalizedSettings[propertyName]);
            }
        });

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

        const settings = normalizeThemeSettingsPayload(collectThemeSettingsPayload());
        syncThemeOpacityLabel(settings);
        window.__appThemeDraftSettings = { ...settings };

        if (typeof window.applyThemeSettings === 'function') {
            window.applyThemeSettings(settings);
        }

        return settings;
    }

    function resetThemeSettings() {
        const savedSettings = normalizeThemeSettingsPayload(
            themeSettingsPageState.savedSettings
            || window.__appThemeSavedSettings
            || window.__appThemeSettings
        );

        clearThemeInvalidStates();
        window.__appThemeDraftSettings = null;
        populateThemeForm(savedSettings);
        themeSettingsPageState.savedSettings = { ...savedSettings };
        if (typeof window.applyThemeSettings === 'function') {
            window.applyThemeSettings(savedSettings);
        }
    }

    async function submitThemeSettings() {
        const settings = normalizeThemeSettingsPayload(collectThemeSettingsPayload());
        const validationErrors = validateThemeSettingsPayload(settings);
        if (validationErrors.length > 0) {
            showEmailValidationErrors(validationErrors);
            return false;
        }

        setEmailButtonsBusy(true, {
            activeButtonId: 'theme-save-button'
        });

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
                throw new Error((await extractEmailApiErrors(response)).join(' '));
            }

            const payload = await response.json();
            clearThemeInvalidStates();
            themeSettingsPageState.savedSettings = { ...settings };
            window.__appThemeSavedSettings = { ...settings };
            window.__appThemeDraftSettings = null;
            if (typeof window.persistThemeSettings === 'function') {
                window.persistThemeSettings(settings);
            }
            if (typeof window.applyThemeSettings === 'function') {
                window.applyThemeSettings(settings);
            }
            showEmailToast(
                payload?.message || 'Настройки темы сохранены.',
                'success',
                'Настройки сохранены'
            );
            return true;
        } catch (error) {
            showEmailToast(
                error.message || 'Не удалось сохранить настройки темы.',
                'error',
                'Сохранение не выполнено',
                { duration: 0 }
            );
            return false;
        } finally {
            setEmailButtonsBusy(false);
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

    function bindThemeAction(buttonId, action, scope) {
        const button = document.getElementById(buttonId);
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

        listen(scope, element, eventName, applyThemeDraftState);
    }

    function bindThemeEffectInput(id, scope) {
        bindThemeInput(id, 'change', scope);
        const element = getThemeField(id);
        if (!element) {
            return;
        }

        listen(scope, element, 'change', syncThemeEffectsSummary);
    }

    function bindThemeEffectsPicker(scope) {
        const trigger = getThemeField('theme-effects-toggle');
        if (trigger) {
            listen(scope, trigger, 'click', (event) => {
                event.preventDefault();
                event.stopPropagation();
                const dropdown = document.getElementById('theme-effects-dropdown');
                setThemeEffectsDropdownOpen(dropdown?.classList.contains('is-hidden'));
            });
        }

        listen(scope, document, 'click', (event) => {
            const page = document.querySelector('[data-page="theme-settings-page"]');
            if (!page) {
                return;
            }

            const target = event.target;
            if (target instanceof Element && target.closest('#theme-effects-toggle, #theme-effects-dropdown')) {
                return;
            }

            setThemeEffectsDropdownOpen(false);
        });
        listen(scope, document, 'keydown', (event) => {
            if (event.key === 'Escape') {
                setThemeEffectsDropdownOpen(false);
            }
        });
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

    window.saveThemeSettings = submitThemeSettings;
    window.resetThemeSettings = resetThemeSettings;

    function mountThemeSettingsPage(pageRoot, scope) {
        if (themeSettingsPageState.cleanup) {
            themeSettingsPageState.cleanup();
            themeSettingsPageState.cleanup = null;
        }

        if (!hasThemeSettingsForm()) {
            themeSettingsPageState.isMounted = false;
            return;
        }

        themeSettingsPageState.isMounted = true;
        bindThemeAction('theme-save-button', window.saveThemeSettings, scope);
        bindThemeAction('theme-reset-button', window.resetThemeSettings, scope);
        bindThemeAction('theme-background-image-upload', () => getThemeField('theme-background-image-file')?.click(), scope);
        bindThemeInput('theme-font-color', 'input', scope);
        bindThemeInput('theme-background-color', 'input', scope);
        bindThemeColorPickerCursor('theme-font-color', scope);
        bindThemeColorPickerCursor('theme-background-color', scope);
        bindThemeInput('theme-background-image-opacity', 'input', scope);
        THEME_PERCENT_FIELDS.forEach(([fieldId]) => bindThemeInput(fieldId, 'input', scope));
        THEME_EFFECT_FIELDS.forEach(([fieldId]) => bindThemeEffectInput(fieldId, scope));
        bindThemeEffectsPicker(scope);

        const fileInput = getThemeField('theme-background-image-file');
        if (fileInput) {
            listen(scope, fileInput, 'change', handleThemeImageChange);
        }

        const savedSettings = normalizeThemeSettingsPayload(
            window.__appThemeSavedSettings
            || window.__appThemeSettings
            || collectThemeSettingsPayload()
        );
        const initialSettings = normalizeThemeSettingsPayload(
            window.__appThemeDraftSettings
            || savedSettings
        );
        populateThemeForm(initialSettings);
        themeSettingsPageState.savedSettings = { ...savedSettings };
        window.__appThemeSavedSettings = { ...savedSettings };
        if (window.__appThemeDraftSettings && typeof window.applyThemeSettings === 'function') {
            window.applyThemeSettings(initialSettings);
        }

        const cleanup = () => {
            if (!themeSettingsPageState.isMounted) {
                return;
            }

            themeSettingsPageState.isMounted = false;
            const nextSettings = normalizeThemeSettingsPayload(
                themeSettingsPageState.savedSettings
                || window.__appThemeSavedSettings
                || window.__appThemeSettings
            );
            window.__appThemeDraftSettings = null;

            if (typeof window.applyThemeSettings === 'function') {
                window.applyThemeSettings(nextSettings);
            }
        };

        themeSettingsPageState.cleanup = cleanup;
        if (scope && typeof scope.add === 'function') {
            scope.add(cleanup);
        }
    }

    window.initThemeSettingsPage = function initThemeSettingsPage(root = document, scope = null) {
        const pageRoot = root?.matches?.('[data-page="theme-settings-page"]')
            ? root
            : root?.querySelector?.('[data-page="theme-settings-page"]');
        if (!pageRoot) {
            return;
        }

        mountThemeSettingsPage(pageRoot, scope);
    };

    window.teardownThemeSettingsPage = function teardownThemeSettingsPage() {
        if (themeSettingsPageState.cleanup) {
            themeSettingsPageState.cleanup();
            themeSettingsPageState.cleanup = null;
        }
    };

    if (window.AppPageLifecycle && typeof window.AppPageLifecycle.register === 'function') {
        window.AppPageLifecycle.register(
            'theme-settings-page',
            '.app-page[data-page="theme-settings-page"]',
            mountThemeSettingsPage
        );
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.initThemeSettingsPage(document), { once: true });
    } else {
        window.initThemeSettingsPage(document);
    }
})();
