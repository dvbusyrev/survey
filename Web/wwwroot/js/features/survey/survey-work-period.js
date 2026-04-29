(function () {
    const existingController = window.__surveyWorkPeriodController;
    if (existingController && typeof existingController.destroy === 'function') {
        existingController.destroy();
    }

    const PAGE_SELECTOR = '.app-page[data-page="surveys-list"]';
    const ROOT_SELECTOR = '[data-role="survey-work-period"]';
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
    const instances = new Map();
    let observer = null;

    function pad(value) {
        return String(value).padStart(2, '0');
    }

    function toIso(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return '';
        }

        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    function parseIso(isoValue) {
        const match = String(isoValue || '').trim().match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (!match) {
            return null;
        }

        const year = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const day = Number.parseInt(match[3], 10);
        const date = new Date(year, month - 1, day);

        if (Number.isNaN(date.getTime())
            || date.getFullYear() !== year
            || date.getMonth() !== month - 1
            || date.getDate() !== day) {
            return null;
        }

        return date;
    }

    function compareIso(left, right) {
        if (!left || !right) {
            return 0;
        }

        return left === right ? 0 : (left > right ? 1 : -1);
    }

    function shiftMonth(sourceDate, monthOffset) {
        const date = sourceDate instanceof Date
            ? new Date(sourceDate.getFullYear(), sourceDate.getMonth(), 1)
            : new Date();
        date.setMonth(date.getMonth() + monthOffset);
        return new Date(date.getFullYear(), date.getMonth(), 1);
    }

    function clearSelection(state) {
        state.rangeStart = '';
        state.rangeEnd = '';
    }

    function getMonthDescription(year, monthIndex) {
        return `${MONTH_NAMES[monthIndex]} ${year}`;
    }

    function createElement(tagName, className, textContent) {
        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }
        if (textContent !== undefined) {
            element.textContent = textContent;
        }
        return element;
    }

    function showToast(message, type) {
        if (typeof window.siteNotify === 'function') {
            window.siteNotify(message, type);
            return;
        }

        window.alert(message);
    }

    function cleanupDetachedInstances() {
        Array.from(instances.entries()).forEach(([root]) => {
            if (!document.contains(root)) {
                instances.delete(root);
            }
        });
    }

    function getPagesFromNode(node) {
        if (!(node instanceof Element)) {
            return [];
        }

        const pages = [];
        if (node.matches(PAGE_SELECTOR)) {
            pages.push(node);
        }

        node.querySelectorAll(PAGE_SELECTOR).forEach((page) => {
            pages.push(page);
        });

        return pages;
    }

    function setPopoverOpen(instance, isOpen) {
        if (!instance.refs.trigger || !instance.refs.popover) {
            return;
        }

        instance.state.isOpen = Boolean(isOpen);
        instance.refs.trigger.setAttribute('aria-expanded', instance.state.isOpen ? 'true' : 'false');
        instance.refs.popover.classList.toggle('is-hidden', !instance.state.isOpen);

        if (instance.state.isOpen) {
            render(instance);
            window.requestAnimationFrame(() => {
                fitPopoverToViewport(instance);
            });
        }
    }

    function closeAllPopovers(exceptRoot = null) {
        cleanupDetachedInstances();
        instances.forEach((instance, root) => {
            if (root === exceptRoot) {
                return;
            }

            setPopoverOpen(instance, false);
        });
    }

    function fitPopoverToViewport(instance) {
        const popover = instance.refs.popover;
        if (!popover || popover.classList.contains('is-hidden')) {
            return;
        }

        popover.style.left = '0px';
        const rect = popover.getBoundingClientRect();
        const viewportWidth = document.documentElement.clientWidth || window.innerWidth || 0;
        const pageGap = 16;
        let leftOffset = 0;

        if (rect.right > viewportWidth - pageGap) {
            leftOffset -= rect.right - viewportWidth + pageGap;
        }

        if (rect.left + leftOffset < pageGap) {
            leftOffset += pageGap - (rect.left + leftOffset);
        }

        popover.style.left = `${Math.round(leftOffset)}px`;
    }

    function isValidSelection(state) {
        return Boolean(state.rangeStart && state.rangeEnd && compareIso(state.rangeEnd, state.rangeStart) > 0);
    }

    function getDisplayState(state) {
        return {
            start: state.rangeStart || '',
            end: state.rangeEnd || ''
        };
    }

    function buildWeekdayRow() {
        const weekdaysRow = createElement('div', 'survey-period-filter__weekday-row');
        WEEKDAY_NAMES.forEach((weekday) => {
            weekdaysRow.appendChild(createElement('span', 'survey-period-filter__weekday', weekday));
        });
        return weekdaysRow;
    }

    function buildDayButton(instance, isoValue, displayState) {
        const dayButton = createElement('button', 'survey-period-filter__day-button');
        const date = parseIso(isoValue);
        dayButton.type = 'button';
        dayButton.dataset.role = 'survey-work-period-day';
        dayButton.dataset.dateIso = isoValue;
        dayButton.textContent = date ? String(date.getDate()) : '';

        if (date && toIso(new Date()) === isoValue) {
            dayButton.classList.add('is-today');
        }

        if (displayState.start && isoValue === displayState.start) {
            dayButton.classList.add('is-range-start');
        }

        if (displayState.end && isoValue === displayState.end) {
            dayButton.classList.add('is-range-end');
        }

        if (displayState.start && displayState.end && compareIso(isoValue, displayState.start) > 0 && compareIso(isoValue, displayState.end) < 0) {
            dayButton.classList.add('is-in-range');
        }

        if (!displayState.end && displayState.start && isoValue === displayState.start) {
            dayButton.classList.add('is-range-single');
        }

        return dayButton;
    }

    function buildCalendarCard(instance, monthDate, displayState) {
        const card = createElement('div', 'survey-period-filter__calendar-card');
        const title = createElement(
            'h4',
            'survey-period-filter__calendar-title',
            getMonthDescription(monthDate.getFullYear(), monthDate.getMonth())
        );
        const weekdaysRow = buildWeekdayRow();
        const daysGrid = createElement('div', 'survey-period-filter__days-grid');
        const firstDayIndex = (new Date(monthDate.getFullYear(), monthDate.getMonth(), 1).getDay() + 6) % 7;
        const daysInMonth = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0).getDate();

        for (let index = 0; index < firstDayIndex; index += 1) {
            daysGrid.appendChild(createElement('span', 'survey-period-filter__day-placeholder'));
        }

        for (let day = 1; day <= daysInMonth; day += 1) {
            const isoValue = toIso(new Date(monthDate.getFullYear(), monthDate.getMonth(), day));
            daysGrid.appendChild(buildDayButton(instance, isoValue, displayState));
        }

        card.appendChild(title);
        card.appendChild(weekdaysRow);
        card.appendChild(daysGrid);
        return card;
    }

    function render(instance) {
        const { state, refs } = instance;
        const monthDate = new Date(state.viewDate.getFullYear(), state.viewDate.getMonth(), 1);

        refs.label.textContent = getMonthDescription(monthDate.getFullYear(), monthDate.getMonth());
        refs.calendar.textContent = '';
        refs.calendar.appendChild(buildCalendarCard(instance, monthDate, getDisplayState(state)));
        refs.saveButton.disabled = state.isSaving || !isValidSelection(state);
        refs.saveButton.textContent = state.isSaving ? 'Сохранение...' : 'Сохранить';
    }

    function handleDateSelection(instance, isoValue) {
        const { state } = instance;
        if (!parseIso(isoValue)) {
            return;
        }

        if (!state.rangeStart || state.rangeEnd) {
            state.rangeStart = isoValue;
            state.rangeEnd = '';
            render(instance);
            return;
        }

        if (compareIso(isoValue, state.rangeStart) < 0) {
            state.rangeEnd = state.rangeStart;
            state.rangeStart = isoValue;
        } else {
            state.rangeEnd = isoValue;
        }

        render(instance);
    }

    async function refreshSurveyList() {
        if (typeof window.refreshAdminUi === 'function') {
            await window.refreshAdminUi({
                tabName: 'get_surveys',
                fallbackUrl: '/surveys',
                options: {
                    force: true,
                    historyMode: 'replace',
                    scrollMode: 'restore'
                }
            });
            return;
        }

        if (typeof window.refreshAdminTab === 'function') {
            await window.refreshAdminTab('get_surveys', null, {
                force: true,
                historyMode: 'replace',
                scrollMode: 'restore'
            });
            return;
        }

        window.location.assign('/surveys');
    }

    async function saveWorkPeriod(instance) {
        const { state } = instance;
        if (!isValidSelection(state)) {
            showToast('Выберите дату начала и дату конца периода.', 'error');
            return;
        }

        state.isSaving = true;
        render(instance);

        try {
            const response = await fetch('/surveys/active/work-period', {
                method: 'POST',
                headers: {
                    Accept: 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    dateBegin: state.rangeStart,
                    dateEnd: state.rangeEnd
                })
            });

            const responseText = await response.text();
            let payload = null;
            try {
                payload = responseText ? JSON.parse(responseText) : null;
            } catch (error) {
                payload = null;
            }

            if (!response.ok || payload?.success === false) {
                throw new Error(payload?.message || responseText || 'Не удалось сохранить период работы.');
            }

            setPopoverOpen(instance, false);
            showToast(payload?.message || 'Период работы активных анкет сохранён.', 'success');
            await refreshSurveyList();
        } catch (error) {
            showToast(error.message || 'Не удалось сохранить период работы.', 'error');
        } finally {
            state.isSaving = false;
            if (document.contains(instance.root)) {
                render(instance);
            }
        }
    }

    function bindInstance(root) {
        if (!(root instanceof Element) || instances.has(root)) {
            return;
        }

        const page = root.closest(PAGE_SELECTOR);
        if (!page) {
            return;
        }

        const today = new Date();
        const instance = {
            root,
            page,
            state: {
                isOpen: false,
                isSaving: false,
                viewDate: new Date(today.getFullYear(), today.getMonth(), 1),
                rangeStart: '',
                rangeEnd: ''
            },
            refs: {
                trigger: root.querySelector('[data-role="survey-work-period-trigger"]'),
                popover: root.querySelector('[data-role="survey-work-period-popover"]'),
                label: root.querySelector('[data-role="survey-work-period-label"]'),
                calendar: root.querySelector('[data-role="survey-work-period-calendar"]'),
                saveButton: root.querySelector('[data-role="survey-work-period-save"]')
            },
            handlers: {}
        };

        if (!instance.refs.trigger || !instance.refs.popover || !instance.refs.label || !instance.refs.calendar || !instance.refs.saveButton) {
            return;
        }

        instance.handlers.click = function (event) {
            const trigger = event.target.closest('[data-role="survey-work-period-trigger"]');
            if (trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers(shouldOpen ? root : null);
                setPopoverOpen(instance, shouldOpen);
                return;
            }

            if (event.target.closest('[data-role="survey-work-period-close"]')) {
                event.preventDefault();
                setPopoverOpen(instance, false);
                return;
            }

            if (event.target.closest('[data-role="survey-work-period-prev"]')) {
                event.preventDefault();
                instance.state.viewDate = shiftMonth(instance.state.viewDate, -1);
                clearSelection(instance.state);
                render(instance);
                return;
            }

            if (event.target.closest('[data-role="survey-work-period-next"]')) {
                event.preventDefault();
                instance.state.viewDate = shiftMonth(instance.state.viewDate, 1);
                clearSelection(instance.state);
                render(instance);
                return;
            }

            const dayButton = event.target.closest('[data-role="survey-work-period-day"]');
            if (dayButton && root.contains(dayButton)) {
                event.preventDefault();
                handleDateSelection(instance, dayButton.dataset.dateIso || '');
                return;
            }

            if (event.target.closest('[data-role="survey-work-period-save"]')) {
                event.preventDefault();
                saveWorkPeriod(instance);
            }
        };

        root.addEventListener('click', instance.handlers.click);
        instances.set(root, instance);
        render(instance);
    }

    function bindAvailablePages(root = document) {
        cleanupDetachedInstances();
        const pages = root === document
            ? Array.from(document.querySelectorAll(PAGE_SELECTOR))
            : getPagesFromNode(root);

        pages.forEach((page) => {
            const workPeriodRoot = page.querySelector(ROOT_SELECTOR);
            if (workPeriodRoot) {
                bindInstance(workPeriodRoot);
            }
        });
    }

    function handleDocumentClick(event) {
        cleanupDetachedInstances();

        let clickedInsideControl = false;
        instances.forEach((instance, root) => {
            if (root.contains(event.target)) {
                clickedInsideControl = true;
            }
        });

        if (!clickedInsideControl) {
            closeAllPopovers();
        }
    }

    function handleDocumentKeydown(event) {
        if (event.key === 'Escape') {
            closeAllPopovers();
        }
    }

    function handleWindowResize() {
        instances.forEach((instance) => {
            if (instance.state.isOpen) {
                fitPopoverToViewport(instance);
            }
        });
    }

    function destroy() {
        instances.forEach((instance, root) => {
            if (instance.handlers?.click) {
                root.removeEventListener('click', instance.handlers.click);
            }
        });
        instances.clear();

        if (observer) {
            observer.disconnect();
            observer = null;
        }

        document.removeEventListener('click', handleDocumentClick, true);
        document.removeEventListener('keydown', handleDocumentKeydown);
        window.removeEventListener('resize', handleWindowResize);
    }

    window.__surveyWorkPeriodController = {
        destroy
    };

    document.addEventListener('click', handleDocumentClick, true);
    document.addEventListener('keydown', handleDocumentKeydown);
    window.addEventListener('resize', handleWindowResize);

    if (typeof MutationObserver !== 'undefined' && document.body) {
        observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    bindAvailablePages(node);
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            bindAvailablePages(document);
        });
        return;
    }

    bindAvailablePages(document);
})();
