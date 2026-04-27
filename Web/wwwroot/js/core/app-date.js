(function () {
    if (window.AppDate) {
        return;
    }

    const DISPLAY_PLACEHOLDER = 'ДД.ММ.ГГГГ';
    const ACTIVE_DATE_FORMAT = 'dd.mm.yyyy';
    const LEGACY_DATE_FORMAT = 'dd/mm/yyyy';
    const GLOBAL_YEAR_RANGE = 10;
    const DATE_INPUT_SELECTOR = `input[type="date"]:not([data-date-proxy="true"]), input[data-date-format="${ACTIVE_DATE_FORMAT}"], input[data-date-format="${LEGACY_DATE_FORMAT}"]`;

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function normalizeInput(value) {
        return String(value || '').trim();
    }

    function createIsoString(year, month, day) {
        return `${year}-${pad(month)}-${pad(day)}`;
    }

    function shiftDateByYears(date, yearOffset) {
        const sourceDate = date instanceof Date ? new Date(date.getTime()) : new Date();
        const sourceMonth = sourceDate.getMonth();
        const shiftedDate = new Date(sourceDate.getTime());
        shiftedDate.setFullYear(shiftedDate.getFullYear() + yearOffset);

        while (shiftedDate.getMonth() !== sourceMonth) {
            shiftedDate.setDate(shiftedDate.getDate() - 1);
        }

        return shiftedDate;
    }

    function dateToIso(date) {
        return createIsoString(date.getFullYear(), date.getMonth() + 1, date.getDate());
    }

    function createGlobalDateBounds() {
        const today = new Date();
        return {
            min: dateToIso(shiftDateByYears(today, -GLOBAL_YEAR_RANGE)),
            max: dateToIso(shiftDateByYears(today, GLOBAL_YEAR_RANGE))
        };
    }

    const GLOBAL_DATE_BOUNDS = createGlobalDateBounds();

    function buildDate(year, month, day) {
        const parsedYear = Number.parseInt(year, 10);
        const parsedMonth = Number.parseInt(month, 10);
        const parsedDay = Number.parseInt(day, 10);

        if (!Number.isInteger(parsedYear) || !Number.isInteger(parsedMonth) || !Number.isInteger(parsedDay)) {
            return null;
        }

        const date = new Date(parsedYear, parsedMonth - 1, parsedDay);
        if (Number.isNaN(date.getTime())) {
            return null;
        }

        if (date.getFullYear() !== parsedYear
            || date.getMonth() !== parsedMonth - 1
            || date.getDate() !== parsedDay) {
            return null;
        }

        return date;
    }

    function parseDate(value) {
        if (value instanceof Date) {
            if (Number.isNaN(value.getTime())) {
                return null;
            }

            return buildDate(value.getFullYear(), value.getMonth() + 1, value.getDate());
        }

        const normalized = normalizeInput(value);
        if (!normalized) {
            return null;
        }

        let match = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (match) {
            return buildDate(match[1], match[2], match[3]);
        }

        match = normalized.match(/^(\d{2})[./-](\d{2})[./-](\d{4})$/);
        if (match) {
            return buildDate(match[3], match[2], match[1]);
        }

        return null;
    }

    function toIso(value) {
        const date = parseDate(value);
        if (!date) {
            return '';
        }

        return createIsoString(date.getFullYear(), date.getMonth() + 1, date.getDate());
    }

    function toDisplay(value) {
        const date = parseDate(value);
        if (!date) {
            return normalizeInput(value);
        }

        return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
    }

    function compare(left, right) {
        const leftIso = toIso(left);
        const rightIso = toIso(right);

        if (!leftIso || !rightIso) {
            return null;
        }

        if (leftIso === rightIso) {
            return 0;
        }

        return leftIso > rightIso ? 1 : -1;
    }

    function resolveInput(target) {
        if (!target) {
            return null;
        }

        if (typeof target === 'string') {
            return document.getElementById(target);
        }

        return target;
    }

    function normalizeLabel(label) {
        return String(label || '')
            .replace(/\s+/g, ' ')
            .replace(/[:*]+$/g, '')
            .trim();
    }

    function resolveInputLabel(input) {
        if (!input || !input.id) {
            return '';
        }

        const labels = Array.from(document.querySelectorAll('label[for]'));
        const matchingLabel = labels.find((label) => label.htmlFor === input.id);
        if (matchingLabel?.textContent) {
            return normalizeLabel(matchingLabel.textContent);
        }

        return '';
    }

    function resolveEffectiveBounds(input) {
        const explicitMin = normalizeInput(input?.dataset?.dateMin || input?.min || '');
        const explicitMax = normalizeInput(input?.dataset?.dateMax || input?.max || '');

        let min = GLOBAL_DATE_BOUNDS.min;
        let max = GLOBAL_DATE_BOUNDS.max;

        if (explicitMin && compare(explicitMin, min) > 0) {
            min = explicitMin;
        }

        if (explicitMax && compare(explicitMax, max) < 0) {
            max = explicitMax;
        }

        return { min, max };
    }

    function isIsoWithinRange(input, isoValue) {
        if (!input || !isoValue) {
            return false;
        }

        const { min, max } = resolveEffectiveBounds(input);

        if (min && compare(isoValue, min) < 0) {
            return false;
        }

        if (max && compare(isoValue, max) > 0) {
            return false;
        }

        return true;
    }

    function syncPickerValue(input) {
        if (!input?._appDatePickerProxy) {
            return;
        }

        const isoValue = toIso(input.value);
        input._appDatePickerProxy.value = isoValue || '';
    }

    function updateInputValidationState(input) {
        if (!input) {
            return false;
        }

        const normalizedValue = normalizeInput(input.value);
        if (!normalizedValue) {
            input.classList.remove('invalid');
            syncPickerValue(input);
            return true;
        }

        const isoValue = toIso(normalizedValue);
        if (!isoValue) {
            input.classList.add('invalid');
            syncPickerValue(input);
            return false;
        }

        input.value = toDisplay(isoValue);
        syncPickerValue(input);

        if (!isIsoWithinRange(input, isoValue)) {
            input.classList.add('invalid');
            return false;
        }

        input.classList.remove('invalid');
        return true;
    }

    function getInputError(target, options = {}) {
        const input = resolveInput(target);
        if (!input) {
            return '';
        }

        const normalizedValue = normalizeInput(input.value);
        const label = normalizeLabel(options.label || input.dataset.dateLabel || resolveInputLabel(input) || 'Дата');

        if (!normalizedValue) {
            if (options.required) {
                return `Заполните поле «${label}».`;
            }

            return '';
        }

        const isoValue = toIso(normalizedValue);
        if (!isoValue) {
            return `Укажите корректную дату в поле «${label}» в формате ДД.ММ.ГГГГ.`;
        }

        const { min, max } = resolveEffectiveBounds(input);

        if (min && compare(isoValue, min) < 0) {
            return `Дата в поле «${label}» не может быть раньше ${toDisplay(min)}.`;
        }

        if (max && compare(isoValue, max) > 0) {
            return `Дата в поле «${label}» не может быть позже ${toDisplay(max)}.`;
        }

        return '';
    }

    function focusInput(target) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        input.focus();
        return true;
    }

    function openPicker(input) {
        const picker = input?._appDatePickerProxy;
        if (!picker) {
            return;
        }

        picker.value = toIso(input.value) || '';

        if (typeof picker.showPicker === 'function') {
            picker.showPicker();
            return;
        }

        picker.focus({ preventScroll: true });
        picker.click();
    }

    function preserveInputSpacing(input, wrapper) {
        if (!window.getComputedStyle) {
            return;
        }

        const computed = window.getComputedStyle(input);
        wrapper.style.marginTop = computed.marginTop;
        wrapper.style.marginRight = computed.marginRight;
        wrapper.style.marginBottom = computed.marginBottom;
        wrapper.style.marginLeft = computed.marginLeft;
        input.style.margin = '0';
    }

    function ensureDateField(input) {
        if (input.parentElement?.classList.contains('app-date-field')) {
            return input.parentElement;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'app-date-field';
        preserveInputSpacing(input, wrapper);

        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const icon = document.createElement('span');
        icon.className = 'app-date-field__icon';
        const iconGlyph = document.createElement('i');
        iconGlyph.className = 'fa-solid fa-calendar-days';
        iconGlyph.setAttribute('aria-hidden', 'true');
        icon.appendChild(iconGlyph);

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'app-date-field__button';
        button.setAttribute('aria-label', 'Открыть календарь');
        button.appendChild(icon);

        const picker = document.createElement('input');
        picker.type = 'date';
        picker.className = 'app-date-field__picker';
        picker.tabIndex = -1;
        picker.dataset.dateProxy = 'true';
        picker.setAttribute('aria-label', 'Выбрать дату');
        picker.setAttribute('aria-hidden', 'true');

        const bounds = resolveEffectiveBounds(input);
        picker.min = bounds.min;
        picker.max = bounds.max;

        if (input.dataset.dateStep) {
            picker.step = input.dataset.dateStep;
        }

        picker.addEventListener('change', function () {
            input.value = picker.value ? toDisplay(picker.value) : '';
            input.classList.remove('invalid');
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });

        button.addEventListener('click', function (event) {
            event.preventDefault();
            openPicker(input);
        });

        wrapper.appendChild(button);
        wrapper.appendChild(picker);
        input._appDatePickerProxy = picker;
        return wrapper;
    }

    function setInputValue(target, value) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        input.value = toDisplay(value);
        updateInputValidationState(input);
        return true;
    }

    function getInputIso(target) {
        const input = resolveInput(target);
        if (!input) {
            return '';
        }

        const isoValue = toIso(input.value);
        if (!isoValue || !isIsoWithinRange(input, isoValue)) {
            return '';
        }

        return isoValue;
    }

    function isInputValid(target) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        const normalized = normalizeInput(input.value);
        return !normalized || Boolean(getInputIso(input));
    }

    function enhanceInput(input) {
        if (!input || input.dataset.dateEnhanced === 'true') {
            return;
        }

        input.dataset.dateEnhanced = 'true';
        input.dataset.dateMin = input.min || input.dataset.dateMin || '';
        input.dataset.dateMax = input.max || input.dataset.dateMax || '';
        input.dataset.dateStep = input.step || input.dataset.dateStep || '';

        if (input.type === 'date') {
            input.type = 'text';
        }

        input.inputMode = 'numeric';
        input.pattern = '\\d{2}\\.\\d{2}\\.\\d{4}';
        input.placeholder = DISPLAY_PLACEHOLDER;
        input.autocomplete = 'off';
        input.title = DISPLAY_PLACEHOLDER;
        input.dataset.dateFormat = ACTIVE_DATE_FORMAT;
        input.classList.add('app-date-field__input');

        ensureDateField(input);

        if (input.value) {
            input.value = toDisplay(input.value);
        }

        syncPickerValue(input);

        input.addEventListener('input', function () {
            if (!input.value) {
                input.classList.remove('invalid');
                syncPickerValue(input);
            }
        });

        input.addEventListener('blur', function () {
            updateInputValidationState(input);
        });

        input.addEventListener('keydown', function (event) {
            if ((event.altKey && event.key === 'ArrowDown') || event.key === 'F4') {
                event.preventDefault();
                openPicker(input);
            }
        });
    }

    function enhanceDateInputs(root) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        scope.querySelectorAll(DATE_INPUT_SELECTOR).forEach(enhanceInput);
    }

    function observeDateInputs() {
        if (!document.body || typeof MutationObserver === 'undefined') {
            return;
        }

        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (!(node instanceof Element)) {
                        return;
                    }

                    if (node.matches?.(DATE_INPUT_SELECTOR)) {
                        enhanceInput(node);
                    }

                    enhanceDateInputs(node);
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function observeDateForms() {
        document.addEventListener('submit', function (event) {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            const dateInputs = Array.from(form.querySelectorAll('input[data-date-enhanced="true"]'));
            const firstInvalidInput = dateInputs.find((input) => {
                const hasValue = Boolean(normalizeInput(input.value));
                if (!hasValue) {
                    input.classList.remove('invalid');
                    return false;
                }

                return !updateInputValidationState(input);
            });

            if (firstInvalidInput) {
                event.preventDefault();
                focusInput(firstInvalidInput);
                window.siteNotify?.(getInputError(firstInvalidInput), 'error', { duration: 0 });
                return;
            }

            const restoreQueue = [];
            dateInputs.forEach((input) => {
                const normalized = normalizeInput(input.value);
                if (!normalized) {
                    input.value = '';
                    return;
                }

                const isoValue = toIso(normalized);
                if (!isoValue) {
                    return;
                }

                const displayValue = input.value;
                input.value = isoValue;

                if (displayValue !== isoValue) {
                    restoreQueue.push([input, displayValue]);
                }
            });

            if (restoreQueue.length === 0) {
                return;
            }

            window.setTimeout(function () {
                restoreQueue.forEach(([input, displayValue]) => {
                    if (document.contains(input)) {
                        input.value = displayValue;
                    }
                });
            }, 0);
        }, true);

        document.addEventListener('reset', function (event) {
            const form = event.target;
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            window.setTimeout(function () {
                form.querySelectorAll('input[data-date-enhanced="true"]').forEach((input) => {
                    if (input.value) {
                        input.value = toDisplay(input.value);
                    }

                    input.classList.remove('invalid');
                    syncPickerValue(input);
                });
            }, 0);
        }, true);
    }

    function todayIso() {
        const today = new Date();
        return createIsoString(today.getFullYear(), today.getMonth() + 1, today.getDate());
    }

    window.AppDate = {
        compare,
        enhanceDateInputs,
        focusInput,
        getBounds: () => ({ ...GLOBAL_DATE_BOUNDS }),
        getInputError,
        getInputIso,
        isInputValid,
        parseDate,
        setInputValue,
        toDisplay,
        toIso,
        todayIso
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            enhanceDateInputs(document);
            observeDateInputs();
            observeDateForms();
        });
        return;
    }

    enhanceDateInputs(document);
    observeDateInputs();
    observeDateForms();
})();
