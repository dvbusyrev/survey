(function () {
    const PAGE_SELECTOR = '[data-page="survey-auto-creation"]';

    function getPageRoot(root = document) {
        if (root?.matches?.(PAGE_SELECTOR)) {
            return root;
        }

        return root?.querySelector?.(PAGE_SELECTOR) || document.querySelector(PAGE_SELECTOR);
    }

    function parseBootstrap(root = document) {
        const node = root?.querySelector?.('#survey-auto-creation-bootstrap')
            || document.getElementById('survey-auto-creation-bootstrap');
        if (!node?.textContent) {
            const today = new Date();
            return {
                isEnabled: false,
                previewYear: today.getFullYear(),
                previewMonth: today.getMonth() + 1,
                selectedSurveys: []
            };
        }

        try {
            return JSON.parse(node.textContent.trim());
        } catch (error) {
            console.error('Не удалось прочитать bootstrap автосоздания анкет:', error);
            return { isEnabled: false, selectedSurveys: [] };
        }
    }

    const state = {
        pageRoot: null,
        cleanup: null,
        selectedSurveys: [],
        availableSurveys: null,
        surveyDropdown: null,
        previewYear: 0,
        previewMonth: 0,
        previewRequestId: 0,
        previewAbortController: null,
        previewErrorShown: false
    };

    function getQueryRoot() {
        return state.pageRoot || document;
    }

    function normalizeSurvey(rawSurvey) {
        return {
            id: Number(rawSurvey?.id ?? rawSurvey?.Id ?? rawSurvey?.id_survey ?? 0),
            name: String(rawSurvey?.name ?? rawSurvey?.Name ?? rawSurvey?.name_survey ?? '').trim()
        };
    }

    function cloneSurveys(items) {
        if (!Array.isArray(items)) {
            return [];
        }

        const uniqueByName = new Map();
        items
            .map((item) => normalizeSurvey(item))
            .filter((item) => item.id > 0 && item.name)
            .forEach((item) => {
                const key = item.name.toLocaleLowerCase('ru-RU');
                if (!uniqueByName.has(key)) {
                    uniqueByName.set(key, item);
                }
            });

        return Array.from(uniqueByName.values());
    }

    function createElement(tagName, className, text) {
        return window.AppUi.createElement(tagName, { className, text });
    }

    function getPreviewTargetDate() {
        return new Date(state.previewYear, state.previewMonth - 1, 1);
    }

    function setPreviewTargetDate(date) {
        state.previewYear = date.getFullYear();
        state.previewMonth = date.getMonth() + 1;
    }

    function shiftPreviewMonth(offset) {
        const target = getPreviewTargetDate();
        target.setMonth(target.getMonth() + offset);
        setPreviewTargetDate(target);
    }

    function buildCalendarWeekdays() {
        const row = createElement('div', 'survey-period-filter__weekday-row');
        window.SurveyFilterCore.WEEKDAY_NAMES.forEach((weekday) => {
            row.appendChild(createElement('span', 'survey-period-filter__weekday', weekday));
        });
        return row;
    }

    function buildCalendarDay(isoValue, startDate, endDate) {
        const date = window.SurveyFilterCore.parseIso(isoValue);
        const day = createElement(
            'button',
            'survey-period-filter__day-button survey-auto-creation-page__calendar-day',
            date ? String(date.getDate()) : ''
        );
        day.type = 'button';
        day.tabIndex = -1;
        day.setAttribute('aria-hidden', 'true');

        const compare = window.SurveyFilterCore.compareIso;
        const inRange = startDate && endDate
            && compare(isoValue, startDate) >= 0
            && compare(isoValue, endDate) <= 0;
        day.classList.toggle('is-in-range', inRange);
        day.classList.toggle('is-range-start', isoValue === startDate);
        day.classList.toggle('is-range-end', isoValue === endDate);
        day.classList.toggle('is-range-single', startDate === endDate && isoValue === startDate);
        return day;
    }

    function buildCalendarCard(monthDate, startDate, endDate) {
        const core = window.SurveyFilterCore;
        const card = createElement('div', 'survey-period-filter__calendar-card');
        const title = createElement(
            'h4',
            'survey-period-filter__calendar-title',
            core.getMonthDescription(monthDate.getFullYear(), monthDate.getMonth())
        );
        const days = createElement('div', 'survey-period-filter__days-grid');
        const firstDayIndex = (new Date(monthDate.getFullYear(), monthDate.getMonth(), 1).getDay() + 6) % 7;
        const daysInMonth = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0).getDate();

        for (let index = 0; index < firstDayIndex; index += 1) {
            days.appendChild(createElement('span', 'survey-period-filter__day-placeholder'));
        }

        for (let day = 1; day <= daysInMonth; day += 1) {
            const isoValue = core.toIso(new Date(monthDate.getFullYear(), monthDate.getMonth(), day));
            days.appendChild(buildCalendarDay(isoValue, startDate, endDate));
        }

        card.appendChild(title);
        card.appendChild(buildCalendarWeekdays());
        card.appendChild(days);
        return card;
    }

    function renderSchedulePreview(result = {}) {
        const root = getQueryRoot();
        const calendars = root.querySelector('[data-role="survey-auto-creation-calendars"]');
        if (!calendars || !window.SurveyFilterCore) {
            return;
        }

        const firstMonth = getPreviewTargetDate();
        const secondMonth = window.SurveyFilterCore.shiftMonth(firstMonth, 1);
        const periods = Array.isArray(result.periods)
            ? result.periods
            : (Array.isArray(result.Periods) ? result.Periods : []);
        const getPeriod = (monthDate) => periods.find((period) => (
            Number(period.year ?? period.Year) === monthDate.getFullYear()
            && Number(period.month ?? period.Month) === monthDate.getMonth() + 1
        )) || {};
        const firstPeriod = getPeriod(firstMonth);
        const secondPeriod = getPeriod(secondMonth);

        calendars.replaceChildren(
            buildCalendarCard(
                firstMonth,
                String(firstPeriod.startDate || firstPeriod.StartDate || ''),
                String(firstPeriod.endDate || firstPeriod.EndDate || '')
            ),
            buildCalendarCard(
                secondMonth,
                String(secondPeriod.startDate || secondPeriod.StartDate || ''),
                String(secondPeriod.endDate || secondPeriod.EndDate || '')
            )
        );
    }

    function showToast(message, type, options = {}) {
        const normalizedMessage = String(message || '').trim();
        if (!normalizedMessage) {
            return;
        }

        window.AppUi.notify(normalizedMessage, type, {
            title: options.title,
            duration: options.duration ?? (type === 'error' ? 0 : 4000)
        });
    }

    function renderSelectedSurveys() {
        const host = getQueryRoot().querySelector('[data-role="survey-auto-creation-selected-list"]');
        if (!host) {
            return;
        }

        host.replaceChildren();

        if (state.selectedSurveys.length === 0) {
            const empty = window.AppUi.createElement('p', {
                className: 'app-field-placeholder survey-auto-creation-page__empty-selection',
                text: 'Анкеты не выбраны'
            });
            host.appendChild(empty);
            return;
        }

        state.selectedSurveys.forEach((survey) => {
            const item = window.AppUi.createElement('div', {
                className: 'survey-auto-creation-page__selected-item',
                text: survey.name
            });
            host.appendChild(item);
        });
    }

    function renderSurveyModalList() {
        const list = getQueryRoot().querySelector('#surveyAutoCreationModalList');
        if (!list) {
            return;
        }

        list.replaceChildren();

        const selectedIds = new Set(state.selectedSurveys.map((survey) => survey.id));
        (state.availableSurveys || []).forEach((survey) => {
            const isSelected = selectedIds.has(survey.id);
            const checkboxOption = window.AppUi.createCheckboxOption({
                text: survey.name,
                checked: isSelected,
                selected: isSelected
            });
            const item = checkboxOption.option;
            const checkbox = checkboxOption.checkbox;

            item.classList.toggle('is-selected', isSelected);
            checkbox.dataset.surveyId = String(survey.id);
            checkbox.addEventListener('change', () => {
                toggleSurveySelection(survey);
                renderSelectedSurveys();
                renderSurveyModalList();
            });

            list.appendChild(item);
        });

        window.AppCheckboxDropdown?.scheduleListHeightUpdate(getSurveyDropdownMenu());
    }

    function setLoading(isLoading) {
        const root = getQueryRoot();
        const loading = root.querySelector('#surveyAutoCreationModalLoading');
        const list = root.querySelector('#surveyAutoCreationModalList');
        if (loading) {
            loading.classList.toggle('u-hidden', !isLoading);
        }
        if (list) {
            list.classList.toggle('u-hidden', isLoading);
        }
    }

    function toggleSurveySelection(survey) {
        const index = state.selectedSurveys.findIndex((item) => item.id === survey.id);
        if (index === -1) {
            state.selectedSurveys.push({ id: survey.id, name: survey.name });
        } else {
            state.selectedSurveys.splice(index, 1);
        }

        state.selectedSurveys.sort((left, right) => left.name.localeCompare(right.name, 'ru'));
    }

    function getSurveyDropdown() {
        return getQueryRoot().querySelector('[data-role="survey-auto-creation-dropdown"]');
    }

    function getSurveyDropdownMenu() {
        const root = getQueryRoot();
        return root.querySelector('[data-role="survey-auto-creation-dropdown-menu"]')
            || root.querySelector('#surveyAutoCreationDropdownMenu')
            || document.getElementById('surveyAutoCreationDropdownMenu');
    }

    function getSurveyDropdownTrigger() {
        return getSurveyDropdown()?.querySelector('[data-role="survey-auto-creation-dropdown-trigger"]')
            || getSurveyDropdown()?.querySelector('button');
    }

    function closeSurveyDropdown() {
        state.surveyDropdown?.controller?.close();
    }

    async function handleSurveyDropdownOpen() {
        if (state.availableSurveys) {
            renderSurveyModalList();
            return;
        }

        setLoading(true);
        try {
            await loadSurveyOptions();
            renderSurveyModalList();
        } catch (error) {
            closeSurveyDropdown();
            showToast(error instanceof Error ? error.message : 'Не удалось загрузить список анкет.', 'error', { title: 'Ошибка' });
        } finally {
            setLoading(false);
        }
    }

    async function loadSurveyOptions() {
        const response = await fetch('/settings/survey-creation/surveys', {
            headers: {
                Accept: 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(
                typeof window.getResponseErrorMessage === 'function'
                    ? window.getResponseErrorMessage(response, 'Не удалось загрузить список анкет.')
                    : `Не удалось загрузить список анкет: ${response.status}`
            );
        }

        const payload = await response.json();
        state.availableSurveys = cloneSurveys(payload).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
    }

    function normalizeBusinessDayInput(input) {
        if (!input) {
            return 0;
        }

        const digits = String(input.value || '').replace(/\D/g, '');
        if (!digits) {
            input.value = '';
            return 0;
        }

        const value = Number.parseInt(digits, 10);
        input.value = String(value);
        return value;
    }

    function readBusinessDayInput(selector) {
        return normalizeBusinessDayInput(getQueryRoot().querySelector(selector));
    }

    function collectRequest() {
        const root = getQueryRoot();
        const reportingPeriod = root.querySelector('#surveyAutoCreationReportingPeriod')?.value || 'month';
        const reportingOffsetBusinessDays = readBusinessDayInput('#surveyAutoCreationReportingOffset');
        const activePeriodBusinessDays = readBusinessDayInput('#surveyAutoCreationActivePeriod');

        return {
            reportingPeriod,
            reportingOffsetBusinessDays,
            activePeriodBusinessDays,
            surveyIds: state.selectedSurveys.map((survey) => survey.id)
        };
    }

    function collectPreviewRequest() {
        const request = collectRequest();
        return {
            reportingPeriod: request.reportingPeriod,
            reportingOffsetBusinessDays: request.reportingOffsetBusinessDays,
            activePeriodBusinessDays: request.activePeriodBusinessDays,
            targetYear: state.previewYear,
            targetMonth: state.previewMonth
        };
    }

    async function postAction(url, payload, signal = null) {
        const options = {
            method: 'POST',
            headers: {
                RequestVerificationToken: window.AppHttp?.getAntiforgeryToken() || ''
            },
            signal
        };

        if (payload !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(payload);
        }

        const response = await fetch(url, options);
        const responseText = await response.text();
        let parsed = null;
        if (responseText) {
            try {
                parsed = JSON.parse(responseText);
            } catch (error) {
                parsed = null;
            }
        }

        if (!response.ok) {
            throw new Error(parsed?.message || parsed?.error || responseText || 'Операция не выполнена.');
        }

        return parsed || { success: true };
    }

    async function refreshSchedulePreview() {
        const calendar = getQueryRoot().querySelector('[data-role="survey-auto-creation-calendar"]');
        const requestId = ++state.previewRequestId;
        state.previewAbortController?.abort();
        state.previewAbortController = new AbortController();
        calendar?.setAttribute('aria-busy', 'true');

        try {
            const result = await postAction(
                '/settings/survey-creation/preview',
                collectPreviewRequest(),
                state.previewAbortController.signal
            );
            if (requestId !== state.previewRequestId) {
                return;
            }

            state.previewErrorShown = false;
            renderSchedulePreview(result);
        } catch (error) {
            if (error?.name === 'AbortError' || requestId !== state.previewRequestId) {
                return;
            }

            if (!state.previewErrorShown) {
                state.previewErrorShown = true;
                showToast(
                    error instanceof Error ? error.message : 'Не удалось рассчитать календарь действия.',
                    'error',
                    { title: 'Ошибка' }
                );
            }
        } finally {
            if (requestId === state.previewRequestId) {
                calendar?.removeAttribute('aria-busy');
            }
        }
    }

    function refreshPage() {
        if (typeof window.refreshAdminUi === 'function') {
            window.refreshAdminUi({
                tabName: 'survey_auto_creation',
                fallbackUrl: '/settings/survey-creation',
                options: {
                    force: true,
                    scrollMode: 'restore'
                }
            });
            return;
        }

        window.location.reload();
    }

    async function submitAction(url, payload, successTitle) {
        try {
            const result = await postAction(url, payload);
            showToast(result.message || 'Операция выполнена.', 'success', { title: successTitle });
            refreshPage();
        } catch (error) {
            showToast(error instanceof Error ? error.message : 'Операция не выполнена.', 'error', { title: 'Ошибка' });
        }
    }

    function mountSurveyDropdownController() {
        state.surveyDropdown?.destroy?.();
        state.surveyDropdown = null;

        const dropdown = getSurveyDropdown();
        const trigger = getSurveyDropdownTrigger();
        const menu = getSurveyDropdownMenu();
        if (!dropdown || !trigger || !menu || typeof window.AppUi?.createMultiselect !== 'function') {
            return;
        }

        trigger.removeAttribute('data-click-call');
        state.surveyDropdown = window.AppUi.createMultiselect({
            root: dropdown,
            trigger,
            menu,
            openClass: 'is-open',
            hiddenClass: 'is-hidden',
            onOpen: () => {
                void handleSurveyDropdownOpen();
                window.AppCheckboxDropdown?.scheduleListHeightUpdate(menu);
            },
            onClose: () => {
                window.AppCheckboxDropdown?.scheduleListHeightUpdate(menu);
            }
        });
    }

    window.saveSurveyAutoCreationSettings = function saveSurveyAutoCreationSettings() {
        return submitAction('/settings/survey-creation/save', collectRequest(), 'Настройки сохранены');
    };

    window.startSurveyAutoCreation = function startSurveyAutoCreation() {
        return submitAction('/settings/survey-creation/start', collectRequest(), 'Автосоздание запущено');
    };

    window.stopSurveyAutoCreation = function stopSurveyAutoCreation() {
        return submitAction('/settings/survey-creation/stop', undefined, 'Автосоздание остановлено');
    };

    function mountSurveyAutoCreationPage(pageRoot, scope) {
        if (state.cleanup) {
            state.cleanup();
            state.cleanup = null;
        }

        if (!pageRoot) {
            return;
        }

        state.pageRoot = pageRoot;
        state.availableSurveys = null;
        const bootstrap = parseBootstrap(pageRoot);
        const today = new Date();
        state.previewYear = Number(bootstrap.previewYear) || today.getFullYear();
        state.previewMonth = Number(bootstrap.previewMonth) || (today.getMonth() + 1);
        state.previewErrorShown = false;
        state.selectedSurveys = cloneSurveys(bootstrap.selectedSurveys).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
        renderSelectedSurveys();
        renderSchedulePreview();
        mountSurveyDropdownController();

        const previewInputs = [
            pageRoot.querySelector('#surveyAutoCreationReportingPeriod'),
            pageRoot.querySelector('#surveyAutoCreationReportingOffset'),
            pageRoot.querySelector('#surveyAutoCreationActivePeriod')
        ].filter(Boolean);
        const previousButton = pageRoot.querySelector('[data-role="survey-auto-creation-calendar-previous"]');
        const nextButton = pageRoot.querySelector('[data-role="survey-auto-creation-calendar-next"]');
        const handlePreviewChange = () => void refreshSchedulePreview();
        const numericInputs = previewInputs.filter((input) => input.matches('[inputmode="numeric"]'));
        const handleNumericInput = (event) => normalizeBusinessDayInput(event.currentTarget);
        const handlePrevious = () => {
            shiftPreviewMonth(-1);
            renderSchedulePreview();
            void refreshSchedulePreview();
        };
        const handleNext = () => {
            shiftPreviewMonth(1);
            renderSchedulePreview();
            void refreshSchedulePreview();
        };

        previewInputs.forEach((input) => input.addEventListener('change', handlePreviewChange));
        numericInputs.forEach((input) => input.addEventListener('input', handleNumericInput));
        previousButton?.addEventListener('click', handlePrevious);
        nextButton?.addEventListener('click', handleNext);
        void refreshSchedulePreview();

        const cleanup = () => {
            state.previewRequestId += 1;
            state.previewAbortController?.abort();
            state.previewAbortController = null;
            previewInputs.forEach((input) => input.removeEventListener('change', handlePreviewChange));
            numericInputs.forEach((input) => input.removeEventListener('input', handleNumericInput));
            previousButton?.removeEventListener('click', handlePrevious);
            nextButton?.removeEventListener('click', handleNext);
            closeSurveyDropdown();
            state.surveyDropdown?.destroy?.();
            state.surveyDropdown = null;
            if (state.pageRoot === pageRoot) {
                state.pageRoot = null;
                state.availableSurveys = null;
            }
        };

        state.cleanup = cleanup;
        if (scope && typeof scope.add === 'function') {
            scope.add(cleanup);
        }
    }

    window.initSurveyAutoCreationPage = function initSurveyAutoCreationPage(root = document, scope = null) {
        const pageRoot = getPageRoot(root);
        mountSurveyAutoCreationPage(pageRoot, scope);
    };

    window.teardownSurveyAutoCreationPage = function teardownSurveyAutoCreationPage() {
        if (state.cleanup) {
            state.cleanup();
            state.cleanup = null;
        }
    };

    if (window.AppPageLifecycle && typeof window.AppPageLifecycle.register === 'function') {
        window.AppPageLifecycle.register(
            'survey-auto-creation-page',
            `.app-page${PAGE_SELECTOR}`,
            mountSurveyAutoCreationPage
        );
    } else if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.initSurveyAutoCreationPage(document), { once: true });
    } else {
        window.initSurveyAutoCreationPage(document);
    }
})();
