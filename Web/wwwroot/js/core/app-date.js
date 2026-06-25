(function () {
    if (window.AppDate) {
        return;
    }

    const DISPLAY_PLACEHOLDER = 'ДД.ММ.ГГГГ';
    const ACTIVE_DATE_FORMAT = 'dd.mm.yyyy';
    const LEGACY_DATE_FORMAT = 'dd/mm/yyyy';
    const DATE_LOCALE = 'ru-RU';
    const GLOBAL_YEAR_RANGE = 10;
    const DATE_INPUT_SELECTOR = `input[type="date"]:not([data-date-proxy="true"]):not([data-date-native="true"]), input[data-date-format="${ACTIVE_DATE_FORMAT}"]:not([data-date-native="true"]), input[data-date-format="${LEGACY_DATE_FORMAT}"]:not([data-date-native="true"])`;
    const NATIVE_DATE_INPUT_SELECTOR = 'input[type="date"][data-date-native="true"]:not([data-date-proxy="true"])';
    const MONTH_NAMES = [
        'Январь',
        'Февраль',
        'Март',
        'Апрель',
        'Май',
        'Июнь',
        'Июль',
        'Август',
        'Сентябрь',
        'Октябрь',
        'Ноябрь',
        'Декабрь'
    ];
    const WEEKDAY_NAMES = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];
    const PICKER_CURSOR_SUPPRESSED_CLASS = 'app-picker-cursor-suppressed';
    const pickerCursorSuppressedElements = new Set();
    let activeCalendarInput = null;

    function isPointerOutsideElement(event, element) {
        if (!event || !element || typeof element.getBoundingClientRect !== 'function') {
            return true;
        }

        const rect = element.getBoundingClientRect();
        return event.clientX < rect.left
            || event.clientX > rect.right
            || event.clientY < rect.top
            || event.clientY > rect.bottom;
    }

    function releasePickerTriggerCursor(element) {
        if (!element?.classList) {
            return;
        }

        element.classList.remove(PICKER_CURSOR_SUPPRESSED_CLASS);
        pickerCursorSuppressedElements.delete(element);
    }

    function releasePickerTriggerCursors(event = null) {
        Array.from(pickerCursorSuppressedElements).forEach((element) => {
            if (!document.contains(element) || !event || isPointerOutsideElement(event, element)) {
                releasePickerTriggerCursor(element);
            }
        });
    }

    function suppressPickerTriggerCursor(element) {
        if (!element?.classList) {
            return;
        }

        element.classList.add(PICKER_CURSOR_SUPPRESSED_CLASS);
        pickerCursorSuppressedElements.add(element);
    }

    window.AppPickerCursor = {
        release: releasePickerTriggerCursor,
        releaseAll: releasePickerTriggerCursors,
        suppress: suppressPickerTriggerCursor
    };

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

    function isNativeDateInput(input) {
        return input?.dataset?.dateNative === 'true' && input.type === 'date';
    }

    function isNativeDateField(input) {
        return input?.dataset?.dateNative === 'true';
    }

    function updateInputValidationState(input) {
        if (!input) {
            return false;
        }

        const normalizedValue = normalizeInput(input.value);
        if (!normalizedValue) {
            input.classList.remove('invalid');
            syncPickerValue(input);
            if (isNativeDateField(input)) {
                syncNativeDatePlaceholder(input);
            }
            return true;
        }

        const isoValue = toIso(normalizedValue);
        if (!isoValue) {
            input.classList.add('invalid');
            syncPickerValue(input);
            if (isNativeDateField(input)) {
                syncNativeDatePlaceholder(input);
            }
            return false;
        }

        input.value = isNativeDateInput(input) ? isoValue : toDisplay(isoValue);
        syncPickerValue(input);
        if (isNativeDateField(input)) {
            syncNativeDatePlaceholder(input);
        }

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
            return isNativeDateInput(input)
                ? `Выберите корректную дату в поле «${label}».`
                : `Укажите корректную дату в поле «${label}» в формате ДД.ММ.ГГГГ.`;
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

    function applyDateLocale(element) {
        if (!element) {
            return;
        }

        element.lang = DATE_LOCALE;
        element.setAttribute('lang', DATE_LOCALE);
        element.setAttribute('data-date-locale', DATE_LOCALE);
    }

    function applyDocumentDateLocale() {
        applyDateLocale(document.documentElement);
        applyDateLocale(document.body);
    }

    function applyDateMask(input) {
        if (!input) {
            return;
        }

        const rawValue = String(input.value || '');
        const value = normalizeInput(rawValue);
        if (!value) {
            return;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
            input.value = toDisplay(value);
            return;
        }

        const canPreserveSelection = document.activeElement === input
            && typeof input.selectionStart === 'number'
            && typeof input.selectionEnd === 'number'
            && typeof input.setSelectionRange === 'function';
        const selectionStart = canPreserveSelection ? input.selectionStart : 0;
        const selectionEnd = canPreserveSelection ? input.selectionEnd : 0;
        const digitsBeforeStart = canPreserveSelection
            ? rawValue.slice(0, selectionStart).replace(/\D/g, '').length
            : 0;
        const digitsBeforeEnd = canPreserveSelection
            ? rawValue.slice(0, selectionEnd).replace(/\D/g, '').length
            : 0;
        const digits = value.replace(/\D/g, '').slice(0, 8);
        const parts = [];

        if (digits.length > 0) {
            parts.push(digits.slice(0, 2));
        }

        if (digits.length > 2) {
            parts.push(digits.slice(2, 4));
        }

        if (digits.length > 4) {
            parts.push(digits.slice(4, 8));
        }

        const formattedValue = parts.join('.');
        if (input.value === formattedValue) {
            return;
        }

        input.value = formattedValue;

        if (!canPreserveSelection) {
            return;
        }

        const resolveSelectionOffset = function (digitCount) {
            if (digitCount <= 0) {
                return 0;
            }

            let seenDigits = 0;
            for (let index = 0; index < formattedValue.length; index += 1) {
                if (/\d/.test(formattedValue[index])) {
                    seenDigits += 1;
                }

                if (seenDigits >= digitCount) {
                    return index + 1;
                }
            }

            return formattedValue.length;
        };

        const nextStart = resolveSelectionOffset(digitsBeforeStart);
        const nextEnd = selectionStart === selectionEnd
            ? nextStart
            : resolveSelectionOffset(digitsBeforeEnd);

        try {
            input.setSelectionRange(nextStart, nextEnd);
        } catch (error) {
            // Some input types refuse manual selection; keep the formatted value.
        }
    }

    function getCalendarState(input) {
        if (!input._appDateCalendarState) {
            const selectedDate = parseDate(input.value);
            const today = new Date();
            const baseDate = selectedDate || today;
            input._appDateCalendarState = {
                viewMonth: baseDate.getMonth(),
                viewYear: baseDate.getFullYear()
            };
        }

        return input._appDateCalendarState;
    }

    function getDaysInMonth(year, monthIndex) {
        return new Date(year, monthIndex + 1, 0).getDate();
    }

    function getMonthStartOffset(year, monthIndex) {
        return (new Date(year, monthIndex, 1).getDay() + 6) % 7;
    }

    function isMonthOutsideRange(input, year, monthIndex, direction) {
        const firstDateIso = createIsoString(year, monthIndex + 1, 1);
        const lastDateIso = createIsoString(year, monthIndex + 1, getDaysInMonth(year, monthIndex));
        const { min, max } = resolveEffectiveBounds(input);

        if (direction < 0 && min && compare(lastDateIso, min) < 0) {
            return true;
        }

        if (direction > 0 && max && compare(firstDateIso, max) > 0) {
            return true;
        }

        return false;
    }

    function shiftCalendarMonth(input, monthOffset) {
        const state = getCalendarState(input);
        const shifted = new Date(state.viewYear, state.viewMonth + monthOffset, 1);
        state.viewYear = shifted.getFullYear();
        state.viewMonth = shifted.getMonth();
        renderCalendar(input);
        positionCalendar(input);
    }

    function closeCalendar(input = activeCalendarInput) {
        if (!input?._appDateCalendar) {
            activeCalendarInput = null;
            return;
        }

        input._appDateCalendar.classList.add('is-hidden');
        input._appDateCalendar.style.visibility = '';
        input._appDateCalendar.style.left = '';
        input._appDateCalendar.style.top = '';
        input._appDateCalendar.style.width = '';
        input._appDateButton?.setAttribute('aria-expanded', 'false');

        if (activeCalendarInput === input) {
            activeCalendarInput = null;
        }
    }

    function positionCalendar(input) {
        const panel = input?._appDateCalendar;
        if (!panel || panel.classList.contains('is-hidden')) {
            return;
        }

        const inputRect = input.getBoundingClientRect();
        const anchorRect = input._appDateButton?.getBoundingClientRect?.() || inputRect;
        const minViewportGap = 8;
        const panelWidth = Math.min(
            Math.max(inputRect.width, 260),
            window.innerWidth - (minViewportGap * 2)
        );

        panel.style.width = `${panelWidth}px`;

        const panelRect = panel.getBoundingClientRect();
        const left = Math.min(
            Math.max(minViewportGap, anchorRect.left),
            Math.max(minViewportGap, window.innerWidth - panelWidth - minViewportGap)
        );
        let top = inputRect.bottom + 6;

        if (top + panelRect.height > window.innerHeight - minViewportGap) {
            top = inputRect.top - panelRect.height - 6;
        }

        if (top < minViewportGap) {
            top = minViewportGap;
        }

        panel.style.left = `${left}px`;
        panel.style.top = `${top}px`;
    }

    function selectCalendarDate(input, isoValue) {
        if (!input || !isoValue || !isIsoWithinRange(input, isoValue)) {
            return;
        }

        input.value = toDisplay(isoValue);
        input.classList.remove('invalid');
        syncPickerValue(input);
        closeCalendar(input);
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.focus({ preventScroll: true });
    }

    function appendCalendarButton(parent, className, label, iconClass, onClick) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = className;
        button.setAttribute('aria-label', label);

        const icon = document.createElement('i');
        icon.className = iconClass;
        icon.setAttribute('aria-hidden', 'true');
        button.appendChild(icon);
        button.addEventListener('click', onClick);
        parent.appendChild(button);
        return button;
    }

    function renderCalendar(input) {
        const panel = input?._appDateCalendar;
        if (!panel) {
            return;
        }

        const state = getCalendarState(input);
        const selectedIso = toIso(input.value);
        const today = todayIso();
        const previousMonth = new Date(state.viewYear, state.viewMonth - 1, 1);
        const nextMonth = new Date(state.viewYear, state.viewMonth + 1, 1);

        const header = document.createElement('div');
        header.className = 'app-date-field__calendar-header';

        const previousButton = appendCalendarButton(
            header,
            'app-date-field__calendar-nav',
            'Предыдущий месяц',
            'fa-solid fa-chevron-left',
            () => shiftCalendarMonth(input, -1)
        );
        previousButton.disabled = isMonthOutsideRange(input, previousMonth.getFullYear(), previousMonth.getMonth(), -1);

        const title = document.createElement('div');
        title.className = 'app-date-field__calendar-title';
        title.textContent = `${MONTH_NAMES[state.viewMonth]} ${state.viewYear}`;
        header.appendChild(title);

        const nextButton = appendCalendarButton(
            header,
            'app-date-field__calendar-nav',
            'Следующий месяц',
            'fa-solid fa-chevron-right',
            () => shiftCalendarMonth(input, 1)
        );
        nextButton.disabled = isMonthOutsideRange(input, nextMonth.getFullYear(), nextMonth.getMonth(), 1);

        const weekdays = document.createElement('div');
        weekdays.className = 'app-date-field__calendar-weekdays';
        WEEKDAY_NAMES.forEach((weekday) => {
            const item = document.createElement('span');
            item.textContent = weekday;
            weekdays.appendChild(item);
        });

        const days = document.createElement('div');
        days.className = 'app-date-field__calendar-days';

        const offset = getMonthStartOffset(state.viewYear, state.viewMonth);
        for (let index = 0; index < offset; index += 1) {
            const spacer = document.createElement('span');
            spacer.className = 'app-date-field__calendar-spacer';
            days.appendChild(spacer);
        }

        const daysInMonth = getDaysInMonth(state.viewYear, state.viewMonth);
        for (let day = 1; day <= daysInMonth; day += 1) {
            const isoValue = createIsoString(state.viewYear, state.viewMonth + 1, day);
            const dayButton = document.createElement('button');
            dayButton.type = 'button';
            dayButton.className = 'app-date-field__calendar-day';
            dayButton.textContent = String(day);
            dayButton.disabled = !isIsoWithinRange(input, isoValue);
            dayButton.classList.toggle('is-selected', selectedIso === isoValue);
            dayButton.classList.toggle('is-today', today === isoValue);
            dayButton.addEventListener('click', () => selectCalendarDate(input, isoValue));
            days.appendChild(dayButton);
        }

        panel.replaceChildren(header, weekdays, days);
    }

    function openPicker(input) {
        const panel = input?._appDateCalendar;
        if (!panel) {
            return;
        }

        if (activeCalendarInput && activeCalendarInput !== input) {
            closeCalendar(activeCalendarInput);
        }

        const selectedDate = parseDate(input.value);
        const today = new Date();
        const baseDate = selectedDate || today;
        input._appDateCalendarState = {
            viewMonth: baseDate.getMonth(),
            viewYear: baseDate.getFullYear()
        };

        activeCalendarInput = input;
        renderCalendar(input);
        panel.classList.remove('is-hidden');
        panel.style.visibility = 'hidden';
        positionCalendar(input);
        panel.style.visibility = '';
        input._appDateButton?.setAttribute('aria-expanded', 'true');
    }

    function handleCalendarDocumentPointerDown(event) {
        if (!activeCalendarInput) {
            return;
        }

        const wrapper = activeCalendarInput.closest('.app-date-field');
        if (wrapper?.contains(event.target)) {
            return;
        }

        closeCalendar(activeCalendarInput);
    }

    function handleCalendarDocumentKeyDown(event) {
        if (event.key === 'Escape' && activeCalendarInput) {
            closeCalendar(activeCalendarInput);
            event.stopPropagation();
        }
    }

    function handleCalendarViewportChange() {
        if (activeCalendarInput) {
            positionCalendar(activeCalendarInput);
        }
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

    function syncNativeDatePlaceholder(input) {
        if (!input) {
            return;
        }

        const isEmpty = !normalizeInput(input.value);
        const isFocused = document.activeElement === input;
        input.dataset.dateEmpty = isEmpty ? 'true' : 'false';
        input.placeholder = isEmpty && !isFocused ? DISPLAY_PLACEHOLDER : '';
        input.closest('.app-native-date-field')?.classList.toggle('app-native-date-field--empty', isEmpty);
        input.closest('.app-native-date-field')?.classList.toggle('app-native-date-field--focused', isFocused);
    }

    function syncNativePickerConstraints(input, picker) {
        if (!input || !picker) {
            return;
        }

        picker.min = input.dataset.dateMin || input.min || '';
        picker.max = input.dataset.dateMax || input.max || '';
        picker.step = input.dataset.dateStep || input.step || '';
    }

    function openNativeDatePicker(input) {
        const picker = input?._appNativeDatePicker;
        if (!picker) {
            input?.focus();
            return;
        }

        applyDocumentDateLocale();
        applyDateLocale(input);
        applyDateLocale(input.closest('.app-native-date-field'));
        applyDateLocale(picker);
        syncPickerValue(input);
        syncNativePickerConstraints(input, picker);

        try {
            if (typeof picker.showPicker === 'function') {
                picker.showPicker();
                return;
            }
        } catch (error) {
            // Some browsers reject showPicker when they decide there is no user activation.
        }

        picker.focus({ preventScroll: true });
        picker.click();
    }

    function enhanceNativeInput(input) {
        if (!input || input.dataset.dateNativeEnhanced === 'true') {
            return;
        }

        input.dataset.dateNativeEnhanced = 'true';
        input.dataset.dateEnhanced = 'true';
        input.dataset.dateMin = input.min || input.dataset.dateMin || '';
        input.dataset.dateMax = input.max || input.dataset.dateMax || '';
        input.dataset.dateStep = input.step || input.dataset.dateStep || '';

        if (input.type === 'date') {
            input.type = 'text';
        }

        input.inputMode = 'numeric';
        input.pattern = '\\d{2}\\.\\d{2}\\.\\d{4}';
        input.title = DISPLAY_PLACEHOLDER;
        applyDocumentDateLocale();
        applyDateLocale(input);
        input.autocomplete = input.autocomplete || 'off';
        input.classList.add('app-native-date-field__input');

        if (!input.getAttribute('aria-label')) {
            const label = input.dataset.dateLabel || resolveInputLabel(input) || DISPLAY_PLACEHOLDER;
            input.setAttribute('aria-label', `${label}, формат ${DISPLAY_PLACEHOLDER}`);
        }

        if (!input.parentElement?.classList.contains('app-native-date-field')) {
            const wrapper = document.createElement('div');
            wrapper.className = 'app-native-date-field';
            applyDateLocale(wrapper);
            preserveInputSpacing(input, wrapper);

            input.parentNode.insertBefore(wrapper, input);
            wrapper.appendChild(input);

            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'app-native-date-field__button';
            button.setAttribute('aria-label', `Открыть календарь: ${input.dataset.dateLabel || resolveInputLabel(input) || 'Дата'}`);
            const icon = document.createElement('i');
            icon.className = 'fa-regular fa-calendar';
            icon.setAttribute('aria-hidden', 'true');
            button.appendChild(icon);
            wrapper.appendChild(button);

            const picker = document.createElement('input');
            picker.type = 'date';
            picker.className = 'app-native-date-field__picker';
            picker.tabIndex = -1;
            picker.setAttribute('aria-hidden', 'true');
            picker.dataset.dateProxy = 'true';
            applyDateLocale(picker);
            syncNativePickerConstraints(input, picker);
            wrapper.appendChild(picker);

            input._appDatePickerProxy = picker;
            input._appNativeDatePicker = picker;
            input._appNativeDateButton = button;

            button.addEventListener('click', function (event) {
                event.preventDefault();
                suppressPickerTriggerCursor(button);
                openNativeDatePicker(input);
                button.blur();
            });

            picker.addEventListener('change', function () {
                input.value = picker.value ? toDisplay(picker.value) : '';
                input.classList.remove('invalid');
                syncNativeDatePlaceholder(input);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                input.focus({ preventScroll: true });
            });
        }

        if (input.value) {
            input.value = toDisplay(input.value);
        }

        syncPickerValue(input);
        syncNativeDatePlaceholder(input);

        input.addEventListener('input', function () {
            applyDateMask(input);
            syncPickerValue(input);
            syncNativeDatePlaceholder(input);
        });

        input.addEventListener('focus', function () {
            syncNativeDatePlaceholder(input);
        });

        input.addEventListener('change', function () {
            syncPickerValue(input);
            syncNativeDatePlaceholder(input);
        });

        input.addEventListener('blur', function () {
            updateInputValidationState(input);
        });

        input.addEventListener('keydown', function (event) {
            if ((event.altKey && event.key === 'ArrowDown') || event.key === 'F4') {
                event.preventDefault();
                suppressPickerTriggerCursor(input._appNativeDateButton || input);
                openNativeDatePicker(input);
            }
        });
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
        iconGlyph.className = 'fa-regular fa-calendar';
        iconGlyph.setAttribute('aria-hidden', 'true');
        icon.appendChild(iconGlyph);

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'app-date-field__button';
        button.setAttribute('aria-label', 'Открыть календарь');
        button.setAttribute('aria-haspopup', 'dialog');
        button.setAttribute('aria-expanded', 'false');
        button.appendChild(icon);

        const picker = document.createElement('input');
        picker.type = 'date';
        picker.className = 'app-date-field__picker';
        picker.tabIndex = -1;
        picker.dataset.dateProxy = 'true';
        picker.setAttribute('aria-label', 'Выбрать дату');
        picker.setAttribute('aria-hidden', 'true');
        applyDateLocale(input);
        applyDateLocale(picker);

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
            suppressPickerTriggerCursor(button);
            openPicker(input);
            button.blur();
        });

        wrapper.appendChild(button);
        wrapper.appendChild(picker);
        const calendar = document.createElement('div');
        calendar.className = 'app-date-field__calendar is-hidden';
        calendar.setAttribute('role', 'dialog');
        calendar.setAttribute('aria-label', 'Календарь');
        wrapper.appendChild(calendar);
        input._appDatePickerProxy = picker;
        input._appDateButton = button;
        input._appDateCalendar = calendar;
        return wrapper;
    }

    function setInputValue(target, value) {
        const input = resolveInput(target);
        if (!input) {
            return false;
        }

        input.value = isNativeDateInput(input) ? toIso(value) : toDisplay(value);
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

        if (!input.getAttribute('aria-label')) {
            const label = input.dataset.dateLabel || resolveInputLabel(input) || 'Дата';
            input.setAttribute('aria-label', `${label}, формат ${DISPLAY_PLACEHOLDER}`);
        }

        ensureDateField(input);

        if (input.value) {
            input.value = toDisplay(input.value);
        }

        syncPickerValue(input);

        input.addEventListener('input', function () {
            applyDateMask(input);
            if (!input.value) {
                input.classList.remove('invalid');
                syncPickerValue(input);
                return;
            }

            syncPickerValue(input);
            if (activeCalendarInput === input) {
                renderCalendar(input);
                positionCalendar(input);
            }
        });

        input.addEventListener('blur', function () {
            updateInputValidationState(input);
        });

        input.addEventListener('keydown', function (event) {
            if ((event.altKey && event.key === 'ArrowDown') || event.key === 'F4') {
                event.preventDefault();
                suppressPickerTriggerCursor(input._appDateButton);
                openPicker(input);
            }
        });
    }

    function enhanceDateInputs(root) {
        const scope = root && typeof root.querySelectorAll === 'function' ? root : document;
        if (scope.matches?.(NATIVE_DATE_INPUT_SELECTOR)) {
            enhanceNativeInput(scope);
        }

        if (scope.matches?.(DATE_INPUT_SELECTOR)) {
            enhanceInput(scope);
        }

        scope.querySelectorAll(NATIVE_DATE_INPUT_SELECTOR).forEach(enhanceNativeInput);
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
                window.AppUi?.notify?.(getInputError(firstInvalidInput), 'error', { duration: 0 });
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
                form.querySelectorAll('input[data-date-native-enhanced="true"]').forEach(syncNativeDatePlaceholder);
            }, 0);
        }, true);
    }

    function observeCalendarEvents() {
        document.addEventListener('pointerdown', handleCalendarDocumentPointerDown, true);
        document.addEventListener('pointermove', releasePickerTriggerCursors, true);
        document.addEventListener('mousemove', releasePickerTriggerCursors, true);
        document.addEventListener('keydown', handleCalendarDocumentKeyDown, true);
        window.addEventListener('scroll', handleCalendarViewportChange, true);
        window.addEventListener('resize', handleCalendarViewportChange);
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
            observeCalendarEvents();
        });
        return;
    }

    enhanceDateInputs(document);
    observeDateInputs();
    observeDateForms();
    observeCalendarEvents();
})();
