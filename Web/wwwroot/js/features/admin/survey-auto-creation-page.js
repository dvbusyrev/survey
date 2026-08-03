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
            return { isEnabled: false, selectedSurveys: [] };
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
        surveyDropdown: null
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

        return items
            .map((item) => normalizeSurvey(item))
            .filter((item) => item.id > 0 && item.name);
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
                className: 'survey-auto-creation-page__empty-selection',
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
        const response = await fetch('/survey/data', {
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

    function collectRequest() {
        const root = getQueryRoot();
        const creationPattern = root.querySelector('#surveyAutoCreationPattern')?.value || '';
        const startPattern = root.querySelector('#surveyAutoCreationStartPattern')?.value || '';
        const endOffsetValue = root.querySelector('#surveyAutoCreationEndOffset')?.value || '';
        const endOffsetBusinessDays = endOffsetValue ? Number(endOffsetValue) : null;

        return {
            creationPattern,
            startPattern,
            endOffsetBusinessDays,
            surveyIds: state.selectedSurveys.map((survey) => survey.id)
        };
    }

    async function postAction(url, payload) {
        const options = {
            method: 'POST',
            headers: {
                RequestVerificationToken: window.AppHttp?.getAntiforgeryToken() || ''
            }
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
        state.selectedSurveys = cloneSurveys(bootstrap.selectedSurveys).sort((left, right) => left.name.localeCompare(right.name, 'ru'));
        renderSelectedSurveys();
        mountSurveyDropdownController();

        const cleanup = () => {
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
