(function () {
    if (window.SurveyDateFilter) {
        return;
    }

    const {
        MONTH_NAMES,
        WEEKDAY_NAMES,
        toIso,
        parseIso,
        shiftMonth,
        getMonthBounds,
        getYearBounds,
        getDecadeStart,
        getDisplayDate,
        compareIso,
        getRangeDescription,
        getMonthDescription,
        createElement
    } = window.SurveyFilterCore;

    function ensurePopoverHeader(root) {
        const popover = root.querySelector('[data-role="survey-date-filter-popover"]');
        const modeSwitch = root.querySelector('[data-role="survey-date-filter-mode-switch"]');
        if (!popover || !modeSwitch) {
            return;
        }

        let header = popover.querySelector('.survey-period-filter__header');
        if (!header) {
            header = createElement('div', 'survey-period-filter__header');
            popover.insertBefore(header, modeSwitch);
            header.appendChild(modeSwitch);
        }

        if (!modeSwitch.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]')) {
            const yearModeButton = createElement('button', 'app-button app-button--secondary survey-period-filter__mode-button', 'По году');
            yearModeButton.type = 'button';
            yearModeButton.dataset.role = 'survey-date-filter-mode';
            yearModeButton.dataset.mode = 'year';
            modeSwitch.insertBefore(yearModeButton, modeSwitch.firstChild);
        }

        if (!header.querySelector('[data-role="survey-date-filter-close"]')) {
            const closeButton = createElement('button', 'survey-period-filter__close-button modal-close');
            closeButton.type = 'button';
            closeButton.dataset.role = 'survey-date-filter-close';
            closeButton.setAttribute('aria-label', 'Закрыть фильтр');

            const closeIcon = createElement('i', 'fas fa-xmark');
            closeIcon.setAttribute('aria-hidden', 'true');
            closeButton.appendChild(closeIcon);

            header.appendChild(closeButton);
        }

        if (!popover.querySelector('[data-role="survey-date-filter-year-panel"]')) {
            const yearPanel = createElement('div', 'survey-period-filter__panel is-hidden');
            yearPanel.dataset.role = 'survey-date-filter-year-panel';

            const panelNav = createElement('div', 'survey-period-filter__panel-nav');

            const prevButton = createElement('button', 'survey-period-filter__nav-button');
            prevButton.type = 'button';
            prevButton.dataset.role = 'survey-date-filter-year-range-prev';
            prevButton.setAttribute('aria-label', 'Предыдущие годы');
            prevButton.appendChild(createElement('i', 'fas fa-chevron-left'));
            prevButton.firstChild?.setAttribute('aria-hidden', 'true');

            const title = createElement('span', 'survey-period-filter__panel-title');
            title.dataset.role = 'survey-date-filter-year-range-label';

            const nextButton = createElement('button', 'survey-period-filter__nav-button');
            nextButton.type = 'button';
            nextButton.dataset.role = 'survey-date-filter-year-range-next';
            nextButton.setAttribute('aria-label', 'Следующие годы');
            nextButton.appendChild(createElement('i', 'fas fa-chevron-right'));
            nextButton.firstChild?.setAttribute('aria-hidden', 'true');

            panelNav.appendChild(prevButton);
            panelNav.appendChild(title);
            panelNav.appendChild(nextButton);

            const yearsContainer = createElement('div', 'survey-period-filter__years');
            yearsContainer.dataset.role = 'survey-date-filter-years';

            yearPanel.appendChild(panelNav);
            yearPanel.appendChild(yearsContainer);

            const monthPanel = popover.querySelector('[data-role="survey-date-filter-month-panel"]');
            if (monthPanel) {
                popover.insertBefore(yearPanel, monthPanel);
            } else {
                popover.appendChild(yearPanel);
            }
        }
    }

    function getInitialState(page, today, serverFilters = window.SurveyServerFilterState) {
        const state = {
            isOpen: false,
            mode: 'month',
            monthViewYear: today.getFullYear(),
            yearViewStart: getDecadeStart(today.getFullYear()),
            rangeViewDate: new Date(today.getFullYear(), today.getMonth(), 1),
            activeFilterType: 'all',
            activeYear: null,
            activeMonth: null,
            rangeStart: '',
            rangeEnd: ''
        };
        const config = serverFilters.getConfig(page);
        if (!config?.enableDateFilter) {
            return state;
        }

        if (Number.isInteger(config.year)) {
            state.activeFilterType = 'year';
            state.activeYear = config.year;
            state.monthViewYear = config.year;
            state.yearViewStart = getDecadeStart(config.year);
            return state;
        }

        const monthMatch = config.month.match(/^(\d{4})-(\d{2})$/);
        if (monthMatch) {
            const year = Number.parseInt(monthMatch[1], 10);
            const monthIndex = Number.parseInt(monthMatch[2], 10) - 1;
            if (Number.isInteger(year) && Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
                state.activeFilterType = 'month';
                state.activeMonth = { year, monthIndex };
                state.monthViewYear = year;
                state.yearViewStart = getDecadeStart(year);
                return state;
            }
        }

        if (config.dateFrom && config.dateTo) {
            state.activeFilterType = 'range';
            state.rangeStart = config.dateFrom;
            state.rangeEnd = config.dateTo;
            const rangeDate = parseIso(config.dateFrom);
            if (rangeDate) {
                state.rangeViewDate = new Date(rangeDate.getFullYear(), rangeDate.getMonth(), 1);
            }
        }

        return state;
    }

    function getCurrentRangeDisplayState(state) {
        if (state.mode === 'range' && state.rangeStart && !state.rangeEnd) {
            return { start: state.rangeStart, end: '' };
        }

        if (state.rangeStart && state.rangeEnd) {
            return { start: state.rangeStart, end: state.rangeEnd };
        }

        return { start: '', end: '' };
    }

    function getActiveFilterBounds(state) {
        if (state.activeFilterType === 'year' && Number.isInteger(state.activeYear)) {
            return getYearBounds(state.activeYear);
        }

        if (state.activeFilterType === 'month' && state.activeMonth) {
            return getMonthBounds(state.activeMonth.year, state.activeMonth.monthIndex);
        }

        if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            return {
                start: state.rangeStart,
                end: state.rangeEnd
            };
        }

        return null;
    }

    function renderModeSwitch(instance) {
        const { state, refs } = instance;
        refs.yearPanel.classList.toggle('is-hidden', state.mode !== 'year');
        refs.monthPanel.classList.toggle('is-hidden', state.mode !== 'month');
        refs.rangePanel.classList.toggle('is-hidden', state.mode !== 'range');

        refs.yearModeButton.classList.toggle('is-active', state.mode === 'year');
        refs.monthModeButton.classList.toggle('is-active', state.mode === 'month');
        refs.rangeModeButton.classList.toggle('is-active', state.mode === 'range');
    }

    function renderYearPanel(instance) {
        const { state, refs } = instance;
        refs.yearRangeLabel.textContent = `${state.yearViewStart} - ${state.yearViewStart + 9}`;
        refs.yearsContainer.textContent = '';

        for (let year = state.yearViewStart; year < state.yearViewStart + 10; year += 1) {
            const yearButton = createElement('button', 'survey-period-filter__year-button', String(year));
            yearButton.type = 'button';
            yearButton.dataset.role = 'survey-date-filter-year';
            yearButton.dataset.year = String(year);

            if (state.activeFilterType === 'year' && state.activeYear === year) {
                yearButton.classList.add('is-selected');
            }

            refs.yearsContainer.appendChild(yearButton);
        }
    }

    function renderMonthPanel(instance) {
        const { state, refs } = instance;
        refs.yearLabel.textContent = String(state.monthViewYear);
        refs.monthsContainer.textContent = '';

        MONTH_NAMES.forEach((monthName, monthIndex) => {
            const monthButton = createElement('button', 'survey-period-filter__month-button', monthName);
            monthButton.type = 'button';
            monthButton.dataset.role = 'survey-date-filter-month';
            monthButton.dataset.monthIndex = String(monthIndex);

            const isSelected = state.activeFilterType === 'month'
                && state.activeMonth
                && state.activeMonth.year === state.monthViewYear
                && state.activeMonth.monthIndex === monthIndex;
            monthButton.classList.toggle('is-selected', isSelected);

            refs.monthsContainer.appendChild(monthButton);
        });
    }

    function buildWeekdayRow() {
        const weekdaysRow = createElement('div', 'survey-period-filter__weekday-row');
        WEEKDAY_NAMES.forEach((weekday) => {
            weekdaysRow.appendChild(createElement('span', 'survey-period-filter__weekday', weekday));
        });
        return weekdaysRow;
    }

    function buildDayButton(isoValue, displayState) {
        const dayButton = createElement('button', 'survey-period-filter__day-button');
        const date = parseIso(isoValue);
        dayButton.type = 'button';
        dayButton.dataset.role = 'survey-date-filter-day';
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

    function buildCalendarCard(monthDate, displayState) {
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
            daysGrid.appendChild(buildDayButton(isoValue, displayState));
        }

        card.appendChild(title);
        card.appendChild(weekdaysRow);
        card.appendChild(daysGrid);
        return card;
    }

    function renderRangePanel(instance) {
        const { state, refs } = instance;
        const displayState = getCurrentRangeDisplayState(state);
        const firstMonth = new Date(state.rangeViewDate.getFullYear(), state.rangeViewDate.getMonth(), 1);
        const secondMonth = shiftMonth(firstMonth, 1);

        refs.rangeLabel.textContent = `${getMonthDescription(firstMonth.getFullYear(), firstMonth.getMonth())} - ${getMonthDescription(secondMonth.getFullYear(), secondMonth.getMonth())}`;
        refs.calendars.textContent = '';
        refs.calendars.appendChild(buildCalendarCard(firstMonth, displayState));
        refs.calendars.appendChild(buildCalendarCard(secondMonth, displayState));

        if (state.rangeStart && !state.rangeEnd) {
            if (refs.hint) {
                refs.hint.textContent = `Начало диапазона: ${getDisplayDate(state.rangeStart)}. Выберите конечную дату.`;
            }
            return;
        }

        if (state.activeFilterType === 'range' && state.rangeStart && state.rangeEnd) {
            if (refs.hint) {
                refs.hint.textContent = window.SurveyFilterSummary.shouldHideCountSummary(instance.page)
                    ? ''
                    : `Выбран диапазон: ${getRangeDescription(state.rangeStart, state.rangeEnd)}.`;
            }
            return;
        }

        if (refs.hint) {
            refs.hint.textContent = 'Выберите начальную и конечную дату периода.';
        }
    }

    function render(instance) {
        renderModeSwitch(instance);
        renderYearPanel(instance);
        renderMonthPanel(instance);
        renderRangePanel(instance);
    }

    function clear(instance, callbacks) {
        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        instance.state.activeFilterType = 'all';
        instance.state.activeYear = null;
        instance.state.activeMonth = null;
        instance.state.rangeStart = '';
        instance.state.rangeEnd = '';
        render(instance);
        if (serverFilters.isServerPage(instance.page)) {
            serverFilters.syncDateState(instance.page, instance.state);
            serverFilters.navigate(instance.page, 'date');
            return;
        }

        callbacks?.applyFilter?.(instance);
    }

    function applyYear(instance, year, callbacks) {
        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        const { state } = instance;
        const isSameYear = state.activeFilterType === 'year' && state.activeYear === year;

        if (isSameYear) {
            clear(instance, callbacks);
            return;
        }

        state.activeFilterType = 'year';
        state.activeYear = year;
        state.monthViewYear = year;
        state.yearViewStart = getDecadeStart(year);
        render(instance);
        if (serverFilters.isServerPage(instance.page)) {
            serverFilters.syncDateState(instance.page, instance.state);
            serverFilters.navigate(instance.page, 'date');
            return;
        }
        callbacks?.applyFilter?.(instance);
    }

    function applyMonth(instance, monthIndex, callbacks) {
        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        const { state } = instance;
        const isSameMonth = state.activeFilterType === 'month'
            && state.activeMonth
            && state.activeMonth.year === state.monthViewYear
            && state.activeMonth.monthIndex === monthIndex;

        if (isSameMonth) {
            clear(instance, callbacks);
            return;
        }

        state.activeFilterType = 'month';
        state.activeYear = null;
        state.activeMonth = {
            year: state.monthViewYear,
            monthIndex
        };
        render(instance);
        if (serverFilters.isServerPage(instance.page)) {
            serverFilters.syncDateState(instance.page, instance.state);
            serverFilters.navigate(instance.page, 'date');
            return;
        }
        callbacks?.applyFilter?.(instance);
    }

    function handleRangeSelection(instance, isoValue, callbacks) {
        const serverFilters = callbacks?.serverFilters || window.SurveyServerFilterState;
        const { state } = instance;

        if (!state.rangeStart || state.rangeEnd) {
            state.rangeStart = isoValue;
            state.rangeEnd = '';
            state.activeFilterType = 'all';
            render(instance);
            if (serverFilters.isServerPage(instance.page)) {
                return;
            }
            callbacks?.applyFilter?.(instance);
            return;
        }

        if (compareIso(isoValue, state.rangeStart) < 0) {
            state.rangeEnd = state.rangeStart;
            state.rangeStart = isoValue;
        } else {
            state.rangeEnd = isoValue;
        }

        state.activeFilterType = 'range';
        state.activeYear = null;
        render(instance);
        if (serverFilters.isServerPage(instance.page)) {
            serverFilters.syncDateState(instance.page, instance.state);
            serverFilters.navigate(instance.page, 'date');
            return;
        }
        callbacks?.applyFilter?.(instance);
    }

    function createInstance(root, {
        pageSelector,
        serverFilters = window.SurveyServerFilterState,
        filterPopover = window.SurveyFilterPopover,
        closeAllPopovers,
        setPopoverOpen,
        applyFilter
    } = {}) {
        ensurePopoverHeader(root);

        const page = root.closest(pageSelector);
        const tableBody = page?.querySelector('[data-role="main-table"] tbody');
        if (!page || !tableBody) {
            return null;
        }

        const instance = {
            root,
            page,
            state: getInitialState(page, new Date(), serverFilters),
            refs: {
                trigger: root.querySelector('[data-role="survey-date-filter-trigger"]'),
                label: root.querySelector('[data-role="survey-date-filter-label"]'),
                popover: root.querySelector('[data-role="survey-date-filter-popover"]'),
                yearModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="year"]'),
                monthModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="month"]'),
                rangeModeButton: root.querySelector('[data-role="survey-date-filter-mode"][data-mode="range"]'),
                yearPanel: root.querySelector('[data-role="survey-date-filter-year-panel"]'),
                monthPanel: root.querySelector('[data-role="survey-date-filter-month-panel"]'),
                rangePanel: root.querySelector('[data-role="survey-date-filter-range-panel"]'),
                yearRangeLabel: root.querySelector('[data-role="survey-date-filter-year-range-label"]'),
                yearsContainer: root.querySelector('[data-role="survey-date-filter-years"]'),
                yearLabel: root.querySelector('[data-role="survey-date-filter-year-label"]'),
                monthsContainer: root.querySelector('[data-role="survey-date-filter-months"]'),
                rangeLabel: root.querySelector('[data-role="survey-date-filter-range-label"]'),
                hint: root.querySelector('[data-role="survey-date-filter-hint"]'),
                calendars: root.querySelector('[data-role="survey-date-filter-calendars"]'),
                summary: root.querySelector('[data-role="survey-date-filter-summary"]'),
                clearButton: root.querySelector('[data-role="survey-date-filter-clear"]')
            },
            handlers: {},
            dropdownController: null
        };

        const callbacks = { serverFilters, applyFilter };
        const setOpen = (isOpen) => setPopoverOpen?.(instance, isOpen) ?? filterPopover.setOpen(instance, isOpen);

        if (typeof window.AppUi?.createDropdown === 'function'
            && instance.refs.trigger
            && instance.refs.popover) {
            const dropdown = window.AppUi.createDropdown({
                root,
                trigger: instance.refs.trigger,
                menu: instance.refs.popover,
                openClass: 'is-open',
                hiddenClass: 'is-hidden',
                onOpen: () => {
                    closeAllPopovers?.(root);
                    filterPopover.applyOpenState(instance, true);
                },
                onClose: () => {
                    filterPopover.applyOpenState(instance, false);
                }
            });
            instance.dropdownController = dropdown.controller;
        }

        instance.handlers.click = function (event) {
            event.stopPropagation();

            const target = event.target instanceof Element ? event.target : null;
            if (!target) {
                return;
            }

            const trigger = target.closest('[data-role="survey-date-filter-trigger"]');
            if (!instance.dropdownController && trigger && root.contains(trigger)) {
                event.preventDefault();
                const shouldOpen = !instance.state.isOpen;
                closeAllPopovers?.(shouldOpen ? root : null);
                setOpen(shouldOpen);
                return;
            }

            const modeButton = target.closest('[data-role="survey-date-filter-mode"]');
            if (modeButton && root.contains(modeButton)) {
                event.preventDefault();
                instance.state.mode = ['year', 'range'].includes(modeButton.dataset.mode)
                    ? modeButton.dataset.mode
                    : 'month';
                render(instance);
                return;
            }

            const simpleActions = [
                ['survey-date-filter-year-range-prev', () => { instance.state.yearViewStart -= 10; }],
                ['survey-date-filter-year-range-next', () => { instance.state.yearViewStart += 10; }],
                ['survey-date-filter-year-prev', () => { instance.state.monthViewYear -= 1; }],
                ['survey-date-filter-year-next', () => { instance.state.monthViewYear += 1; }],
                ['survey-date-filter-range-prev', () => { instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, -1); }],
                ['survey-date-filter-range-next', () => { instance.state.rangeViewDate = shiftMonth(instance.state.rangeViewDate, 1); }]
            ];

            for (const [role, action] of simpleActions) {
                if (target.closest(`[data-role="${role}"]`)) {
                    event.preventDefault();
                    action();
                    render(instance);
                    return;
                }
            }

            if (target.closest('[data-role="survey-date-filter-close"]')) {
                event.preventDefault();
                setOpen(false);
                return;
            }

            const yearButton = target.closest('[data-role="survey-date-filter-year"]');
            if (yearButton && root.contains(yearButton)) {
                event.preventDefault();
                const selectedYear = Number.parseInt(yearButton.dataset.year || '', 10);
                if (Number.isInteger(selectedYear)) {
                    applyYear(instance, selectedYear, callbacks);
                }
                return;
            }

            const monthButton = target.closest('[data-role="survey-date-filter-month"]');
            if (monthButton && root.contains(monthButton)) {
                event.preventDefault();
                const monthIndex = Number.parseInt(monthButton.dataset.monthIndex || '', 10);
                if (Number.isInteger(monthIndex) && monthIndex >= 0 && monthIndex < 12) {
                    applyMonth(instance, monthIndex, callbacks);
                }
                return;
            }

            const dayButton = target.closest('[data-role="survey-date-filter-day"]');
            if (dayButton && root.contains(dayButton)) {
                event.preventDefault();
                const isoValue = dayButton.dataset.dateIso || '';
                if (parseIso(isoValue)) {
                    handleRangeSelection(instance, isoValue, callbacks);
                }
                return;
            }

            if (target.closest('[data-role="survey-date-filter-clear"]')) {
                event.preventDefault();
                clear(instance, callbacks);
            }
        };

        root.addEventListener('click', instance.handlers.click);
        instance.destroy = function destroyDateFilterInstance() {
            root.removeEventListener('click', instance.handlers.click);
            instance.dropdownController?.destroy?.();
        };

        render(instance);
        applyFilter?.(instance);
        return instance;
    }

    window.SurveyDateFilter = {
        ensurePopoverHeader,
        getInitialState,
        getActiveFilterBounds,
        createInstance,
        render,
        clear,
        applyYear,
        applyMonth,
        handleRangeSelection
    };
})();
